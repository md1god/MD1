@echo off
echo START %date% %time% > D:\CC_GAME_1\ops\shots\run_shot.state
"C:\Program Files\Unity\Hub\Editor\6000.0.84f1\Editor\Unity.exe" -batchmode -quit -projectPath D:\CC_GAME_1 -executeMethod CCGameShot.Run -logFile D:\CC_GAME_1\ops\shots\shot.log
echo EXITCODE=%ERRORLEVEL% >> D:\CC_GAME_1\ops\shots\shot.log
echo DONE %date% %time% >> D:\CC_GAME_1\ops\shots\run_shot.state