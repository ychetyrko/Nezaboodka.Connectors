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
ECHO.
ECHO Usage: %~nx0 ^<action^> ^<username^> [^<password^>]
ECHO.
ECHO Where%TAB%^<action^>:
ECHO %TAB%%TAB%/i%TAB%create Nezaboodka Admin MySQL database
ECHO %TAB%%TAB%/r%TAB%drop all Nezaboodka MySQL databases (need confirmation)
ECHO %TAB%%TAB%/rq%TAB%drop all Nezaboodka MySQL databases (without confirmation)
ECHO.
ECHO %TAB%^<username^>%TAB%MySQL user account name
ECHO %TAB%^<password^>%TAB%MySQL user account password (optional)

:quit
ECHO.
