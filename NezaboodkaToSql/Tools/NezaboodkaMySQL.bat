@ECHO off

IF "%1" == "" GOTO :help
IF "%1" == "/h" GOTO :help
IF "%1" == "/?" GOTO :help

SET "script="
SET "result="

SET "command_argument=%~1"

IF "%command_argument%" == "/i" (
	SET script=%~dp0/scripts/install.sql
	SET result=Created Nezaboodka MySQL database
) ELSE (
	IF "%command_argument:~0,2%" == "/r" ( 
		IF "%command_argument:~2,1%" NEQ "q" (
			SETLOCAL ENABLEDELAYEDEXPANSION
			CHOICE /M "Do you really want to DROP All Nezaboodka Databases"
			IF ERRORLEVEL 1 SET X=1
			IF ERRORLEVEL 2 SET X=2

			IF "!X!" == "2" GOTO :cancel
		)
		
		SET script=%~dp0/scripts/remove.sql
		SET result=Dropped Nezaboodka MySQL database
	)
)

SET PASSWORD=-p

IF "%~3" NEQ "" (
	SET PASSWORD=--password="%~3"
)

IF "%script%" NEQ "" (
	mysql --user="%~2" %PASSWORD% < "%script%" && ECHO %result% || SET ERRORLEVEL=-1
) ELSE (
	SET ERRORLEVEL=-1
	ECHO ERROR: Invalid syntax.
	GOTO :help
)
GOTO :quit

:cancel
ECHO.
ECHO User has canceled dropping.
GOTO :quit

:help
SET "TAB=	"

SET "SCRIPT_NAME=%~nx0"
IF NOT "%SCRIPT_NAME: =%" == "%SCRIPT_NAME%" SET SCRIPT_NAME="%SCRIPT_NAME%"

ECHO.
ECHO Usage format:
ECHO.
ECHO %TAB%%SCRIPT_NAME% ^<action^> ^<username^> [^<password^>]
ECHO.
ECHO Where:
ECHO %TAB%^<action^> - one of the following:
ECHO %TAB%%TAB%/i  - install: create Nezaboodka Admin MySQL database
ECHO %TAB%%TAB%/r  - remove: drop all Nezaboodka MySQL databases (need confirmation)
ECHO %TAB%%TAB%/rq - remove quiet: drop all Nezaboodka MySQL databases (without confirmation)
ECHO.
ECHO %TAB%^<username^> - MySQL user account name
ECHO %TAB%^<password^> - MySQL user account password (optional)
ECHO.

:quit
ECHO.
