# Tesseract (OCR) module

This project uses Tesseract to provide OCR functionality.

## Overview

- Scanner enables OCR by integrating Tesseract.
- The project includes the .NET Tesseract wrapper (charlesw/tesseract) as a git submodule:
  https://github.com/charlesw/tesseract
- The upstream wrapper does not include ARM64 native support. A minimal, local update was made to add ARM64 support so Scanner can run on ARM64 Windows machines.

## What was changed

The update is intentionally small and focused:

- The wrapper was modified to detect when it is running on ARM64 and to load the appropriate native DLLs from an ARM64-specific folder.
  - Detection logic checks the process/OS architecture at runtime and selects the matching native library path (e.g., native/arm64 vs native/x64).
  - Loading behavior was kept simple and limited to selecting the correct DLL files rather than changing wrapper APIs.
- ARM64 native DLLs were compiled and added to the repository under the native/arm64 location (or similar). These are the modified/compiled builds of leptonica/tesseract native libraries required by the wrapper.

This approach keeps the wrapper changes minimal and backwards compatible with existing x86/x64 deployments while enabling ARM64 support.

You can see the changes yourself in this commit: https://github.com/simon-knuth/tesseract-arm64/commit/5cace79c379c851f51c18ef300ddde13acd7f046

## Build notes / provenance

- The ARM64 DLLs were compiled following the compilation guide in the wrapper repo:
  https://github.com/charlesw/tesseract/blob/b5329d5be92fa670031d94c3875f879651a01f55/docs/Compling_tesseract_and_leptonica.md
- The build process follows the same steps as the upstream guide (build leptonica, build tesseract, build native wrapper components), targeting ARM64 toolchains where required.

## Steps to update the ARM64 DLLs (based on original guide)

1. Install Visual Studio 2022
2. Install CMake (ensure it's on your path)
3. Install vcpkg
4. Install dependencies
```
vcpkg install giflib:arm64-windows-static libjpeg-turbo:arm64-windows-static liblzma:arm64-windows-static libpng:arm64-windows-static tiff:arm64-windows-static zlib:arm64-windows-static
```
4. Build Leptonica to get the DLL (assuming version 1.82.0 is desired):
```
git clone https://github.com/DanBloomberg/leptonica.git & cd leptonica	
git checkout -b 1.82.0 1.82.0
mkdir vs22-ARM64 & cd vs22-ARM64
cmake .. -G "Visual Studio 17 2022" -A ARM64 -DSW_BUILD=OFF -DBUILD_SHARED_LIBS=ON -DCMAKE_TOOLCHAIN_FILE=%VCPKG_HOME%\\scripts\\buildsystems\\vcpkg.cmake -DVCPKG_TARGET_TRIPLET=arm64-windows-static -DCMAKE_INSTALL_PREFIX=..\\..\\build\\arm64
cmake --build . --config Release --target install
```
5. Grab the leptonica-1.82.0.dll from the bin folder and insert it into the wrapper's ARM64 folder
6. Build Tesseract (not the wrapper, assuming version 5.2.0 is desired) to get the exe and DLL
```
git clone https://github.com/tesseract-ocr/tesseract.git
cd tesserct
git checkout -b 5.2.0 5.2.0
mkdir vs22-ARM64 & cd vs22-ARM64
cmake .. -G "Visual Studio 17 2022" -A ARM64 -DBUILD_SHARED_LIBS=ON -DAUTO_OPTIMIZE=OFF -DSW_BUILD=OFF -DBUILD_TRAINING_TOOLS=OFF -DCMAKE_PREFIX_PATH={{PATH_TO_LEPTONICA_REPO}}\leptonica\vs22-ARM64 -DCMAKE_INSTALL_PREFIX=..\build\ARM64
cmake --build . --config Release --target install
```
7. Grab the tesseract.exe and tesseract DLL from the bin folder and insert it into the wrapper's ARM64 folder
8. Rename the tesseract DLL to tesseract50.dll