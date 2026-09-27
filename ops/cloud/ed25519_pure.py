"""
Ed25519 signing, pure stdlib. RFC 8032 reference algorithm.

Why: the OpenClaw Gateway requires every WebSocket client to present a *signed
device identity* (Ed25519). The cloud box has no crypto library and pip on it is
not ours to change, and the local box here has neither `cryptography` nor
`nacl` installed. The reference algorithm is public-domain textbook math, so we
just implement it.

Verified below against the RFC 8032 section 7.1 test vectors -- if those pass,
the signatures are real Ed25519 and not a lookalike.

Encoding used by OpenClaw (read out of assets/nodes-*.js):
    bytes  -> base64url, no padding      (fn H)
    b64url -> bytes                       (fn U)
    deviceId = lowercase hex of SHA-256(raw 32-byte public key)
"""
import hashlib

P = 2 ** 255 - 19
L = 2 ** 252 + 27742317777372353535851937790883648493
D = -121665 * pow(121666, P - 2, P) % P
I = pow(2, (P - 1) // 4, P)


def _sha512(b):
    return hashlib.sha512(b).digest()


def _inv(x):
    return pow(x, P - 2, P)


def _recover_x(y, sign):
    if y >= P:
        return None
    x2 = (y * y - 1) * _inv(D * y * y + 1) % P
    if x2 == 0:
        return None if sign else 0
    x = pow(x2, (P + 3) // 8, P)
    if (x * x - x2) % P != 0:
        x = x * I % P
    if (x * x - x2) % P != 0:
        return None
    if (x & 1) != sign:
        x = P - x
    return x


_BY = 4 * _inv(5) % P
_BX = _recover_x(_BY, 0)
G = (_BX, _BY, 1, _BX * _BY % P)          # extended coords: (X, Y, Z, T)


def _add(pt, qt):
    a = (pt[1] - pt[0]) * (qt[1] - qt[0]) % P
    b = (pt[1] + pt[0]) * (qt[1] + qt[0]) % P
    c = 2 * pt[3] * qt[3] * D % P
    dd = 2 * pt[2] * qt[2] % P
    e, f, g, h = b - a, dd - c, dd + c, b + a
    return (e * f % P, g * h % P, f * g % P, e * h % P)


def _mul(s, pt):
    q = (0, 1, 1, 0)                        # neutral element
    while s > 0:
        if s & 1:
            q = _add(q, pt)
        pt = _add(pt, pt)
        s >>= 1
    return q


def _encode_point(pt):
    zi = _inv(pt[2])
    x, y = pt[0] * zi % P, pt[1] * zi % P
    return int.to_bytes(y | ((x & 1) << 255), 32, "little")


def _secret_scalar(seed32):
    """Prune SHA-512(seed)[0:32] per RFC 8032 5.1.5 and read it little-endian."""
    h = _sha512(seed32)
    a = 2 ** 254 + sum(2 ** i * ((h[i // 8] >> (i % 8)) & 1) for i in range(3, 254))
    return a


def _secret_prefix(seed32):
    """SHA-512(seed)[32:64] -- the second half, used to derive the nonce scalar."""
    return _sha512(seed32)[32:]


def public_key(seed32):
    return _encode_point(_mul(_secret_scalar(seed32), G))


def sign(seed32, msg):
    """RFC 8032 Ed25519 signature over `msg`.

    Verified byte-for-byte against the three section 7.1 vectors in _selftest().

        a = clamp(SHA512(seed)[0:32])          A = [a]B
        r = int(SHA512(SHA512(seed)[32:64] || msg)) mod L
        R = [r]B
        k = int(SHA512(R || A || msg)) mod L   <- all 64 digest bytes
        S = (r + k*a) mod L
    """
    a = _secret_scalar(seed32)
    pk = public_key(seed32)
    # r is derived from the *upper half* of SHA-512(seed) used as a hash prefix
    # in front of the message, then the whole 64-byte digest read little-endian.
    r = int.from_bytes(_sha512(_secret_prefix(seed32) + msg), "little") % L
    rp = _encode_point(_mul(r, G))
    k = int.from_bytes(_sha512(rp + pk + msg), "little") % L
    return rp + int.to_bytes((r + k * a) % L, 32, "little")


# ---------------------------------------------------------------- OpenClaw glue
def b64u(raw):
    import base64
    return base64.urlsafe_b64encode(raw).decode().rstrip("=")


def unb64u(text):
    import base64
    t = text.replace("-", "+").replace("_", "/")
    t += "=" * ((4 - len(t) % 4) % 4)
    return base64.b64decode(t)


def make_identity(seed32=None):
    """Return the (deviceId, publicKey_b64u, privateKey_b64u) triple."""
    import os
    if seed32 is None:
        seed32 = os.urandom(32)
    pk = public_key(seed32)
    return {
        "deviceId": hashlib.sha256(pk).hexdigest(),
        "publicKey": b64u(pk),
        "privateKey": b64u(seed32),
    }


def device_message(device_id, client_id, client_mode, role, scopes,
                   signed_at_ms, token, nonce):
    """Reproduce assets/gateway-*.js fn `j` exactly.

        ['v2', deviceId, clientId, clientMode, role,
         scopes.join(','), String(signedAtMs), token ?? '', nonce].join('|')
    """
    return "|".join(["v2", device_id, client_id, client_mode, role,
                     ",".join(scopes), str(int(signed_at_ms)),
                     token or "", nonce or ""])


def _selftest():
    # RFC 8032 section 7.1, vectors quoted verbatim from the RFC text.
    vectors = [
        # TEST 1 -- empty message
        ("9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60",
         "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a",
         "",
         "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e06522490155"
         "5fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b"),
        # TEST 2 -- one byte, 0x72
        ("4ccd089b28ff96da9db6c346ec114e0f5b8a319f35aba624da8cf6ed4fb8a6fb",
         "3d4017c3e843895a92b70aa74d1b7ebc9c982ccf2ec4968cc0cd55f12af4660c",
         "72",
         "92a009a9f0d4cab8720e820b5f642540a2b27b5416503f8fb3762223ebdb69da"
         "085ac1e43e15996e458f3613d0f11d8c387b2eaeb4302aeeb00d291612bb0c00"),
        # TEST 3 -- two bytes, 0xaf82
        ("c5aa8df43f9f837bedb7442f31dcb7b166d38535076f094b85ce3a2e0b4458f7",
         "fc51cd8e6218a1a38da47ed00230f0580816ed13ba3303ac5deb911548908025",
         "af82",
         "6291d657deec24024827e69c3abe01a30ce548a284743a445e3680d7db5ac3ac"
         "18ff9b538d16f290ae67f760984dc6594a7c15e9716ed28dc027beceea1ec40a"),
    ]
    for seed_hex, pk_hex, msg_hex, sig_hex in vectors:
        seed = bytes.fromhex(seed_hex)
        msg = bytes.fromhex(msg_hex)
        assert public_key(seed).hex() == pk_hex, f"publickey failed for seed {seed_hex[:8]}"
        got = sign(seed, msg).hex()
        assert got == sig_hex, (f"signature failed for seed {seed_hex[:8]}\n"
                                f"  got  {got}\n  want {sig_hex}")
    return len(vectors)


if __name__ == "__main__":
    n = _selftest()
    print(f"ed25519_pure: RFC 8032 test vectors PASS ({n} vectors, "
          f"public key + signature)")
    ident = make_identity(b"\x01" * 32)
    print("sample deviceId :", ident["deviceId"])
    print("sample publicKey:", ident["publicKey"])
