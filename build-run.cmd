@echo off
setlocal enabledelayedexpansion enableextensions

for /f "tokens=*" %%f in ('
        forfiles /p "%WINDIR%" /m "explorer.exe" /c "cmd /c echo 0x1B"
        ') do @(
    set clear=%%f[0m
    set red=%%f[91m
    set yellow=%%f[93m
)

set compiler=csc.exe
set found=0
call !compiler! -version 1>NUL 2>NUL & set status=!ERRORLEVEL!
if !status! == 0 (
    for /f "tokens=*" %%f in ('!compiler! -version 2^>NUL') do @(
        set found=1
    )
) else (
    echo Failed to verify that default compiler works... Skipping... 1>&2
)

:tryagain
if !found! == 0 (
    echo !red!You have no compatible C# compiler in your PATH environment variable.!red! !yellow!You might benefit from rewriting this script ^(!clear!"%~dpf0"!yellow!^) with an absolute path to your compiler, or add your preferred compiler's directory to the PATH environment variable.!clear! 1>&2
    echo. 1>&2
    echo In the meantime, this script needs a compiler. Would you like me to search the usual primary storage drives for one ^(this could take several minutes if not hours on a large hard disk drive!^)? 1>&2
    echo. 1>&2
    echo Ctrl+C ^(enough times^) should exit the program. Alternatively, you may enter an absolute ^(rooted with the drive letter^) path to your compiler instead. Or just press enter without entering anything to start the disk search. 1>&2
    echo. 1>&2
    set /p compiler="Enter an absolute path to your C# compiler (csc.exe) [ *search the drive for csc.exe instead * ]> "

    if not defined compiler (
        echo. 1>&2
        echo So you have chosen for the computer to do the hard work this time. Smart. But seriously, it's wasteful, so use the result to fix things for next time. :^) 1>&2

        set compiler=csc.exe
        set largest=

        for /f "tokens=2 delims==" %%d in ('wmic logicaldisk where "drivetype=3" get name /format:value') do @(
            for /f "tokens=*" %%f in ('dir /a /b /s "%%d\csc.exe"') do @(
                echo Intermediate result: found "%%f".. 1>&2

                call "%%f" -version 1>NUL 2>NUL & set status=!ERRORLEVEL!

                if !status! == 0 (
                    for /f "tokens=*" %%g in ('"%%f" -version 2^>NUL') do @(
                        if defined largest (
                            if "%%g" GTR "!largest!" (
                                set "compiler=%%f"
                                set "largest=%%g"
                            )
                        ) else (
                            set "compiler=%%f"
                            set "largest=%%g"
                        )
                    )
                ) else (
                    echo Failed to verify that compiler works... Skipping... 1>&2
                )
            )
        )

        echo I came up with "!compiler!", version "!largest!"! I wish this script knew if that was enough. :^) 1>&2
    ) else (
        if not exist "!compiler!" (
            echo The compiler you specified does not exist: "!compiler!". Please try again. 1>&2
            goto :tryagain
        )
    )
)

call "!compiler!" /debug Program.cs || exit /b !ERRORLEVEL!
Program.exe %*
