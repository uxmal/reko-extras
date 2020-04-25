#!/bin/bash
echo -ne "" | gcc -x c - -shared -o libopcodes.dll -L${MINGW_PREFIX}/lib/binutils -Wl,--whole-archive -lopcodes -Wl,--no-whole-archive -lintl -liberty
