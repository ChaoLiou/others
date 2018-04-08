SET /P REPOSITORY_NAME=REPOSITORY NAME(NEW REPOSITORY NAME):
SET /P FOLDER_NAME=FOLDER_NAME:
SET BRANCH_NAME=master

IF NOT EXIST %FOLDER_NAME% (mkdir %FOLDER_NAME%)
cd %FOLDER_NAME%

IF NOT EXIST %REPOSITORY_NAME% (git clone http://teamserver:8080/tfs/DefaultCollection/!Backlog/_git/%REPOSITORY_NAME%)
IF NOT ["%errorlevel%"]==["0"] (
pause
exit /b %errorlevel%
)
cd %REPOSITORY_NAME%

git filter-branch --prune-empty --subdirectory-filter %FOLDER_NAME% %BRANCH_NAME%
IF NOT ["%errorlevel%"]==["0"] (
pause
exit /b %errorlevel%
)

git remote -v
git remote set-url origin http://teamserver:8080/tfs/DefaultCollection/rdlab/_git/%FOLDER_NAME%
IF NOT ["%errorlevel%"]==["0"] (
pause
exit /b %errorlevel%
)
git remote -v

git push -u origin %BRANCH_NAME%
IF NOT ["%errorlevel%"]==["0"] (
pause
exit /b %errorlevel%
)

pause