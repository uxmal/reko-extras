@echo off 
rem After running chunks, collects automatically generated unit tests and
rem places them in the `tests` subdirectory.
copy /y bin\Release\net5.0\*.tests tests
