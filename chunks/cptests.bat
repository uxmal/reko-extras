@echo off 
rem After running chunks, collects automatically generated unit tests and
rem places them in the `tests` subdirectory.
copy /y bin\Release\net6.0\*.tests tests
