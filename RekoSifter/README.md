## RekoSifter
A tool inspired by sandsifter (https://github.com/xoreaxeaxeax/sandsifter) to test and fuzz Reko disassemblers

## Setup
The project runs on .NET Core, but is currently limited to Windows operating systems for native interop reasons.
If the need arises, it can be made compatible with Linux too.

### LLVM
The easiest way to prepare an LLVM setup is to install msys2 and install the `llvm-svn` package.

This will install `libLLVM`, required by LLVMSharp.

### ObjDump
The objdump setup requires a binutils build environment. It can be prepared on msys2 or cygwin,
but it will take considerably longer to compile binutils.

WSL or a Virtual machine with mingw cross compilers is recommended.

Follow any of the tutorials for preparing the binutils build environment. You can download the desired binutils versions from https://ftp.gnu.org/gnu/binutils/

Copy `build-all.sh` and `build.sh` from `scripts` in the untarred binutils directory

Now run `build-all.sh` to compile `libopcodes` into a `.dll` for each target (target list is specified in the script itself)

If you want to re-generate the `BfdMachine` enum, you can use `make-bfdmach.php` (NOTE: requires a little manual fixup for a couple of lines)

```php make-bfdmach.php /usr/include/bfd.h```
