#!/usr/bin/env bash
# Candidate-only build. Does not replace the pinned shipping encoder.
# Run in an extracted parent containing ffmpeg-7.1 and zlib-1.3.2.
# Requires LLVM-MinGW on PATH and Git Bash utilities.
set -euo pipefail
export SOURCE_DATE_EPOCH=1727740800
mkdir -p compiler-tmp
export TMPDIR="$PWD/compiler-tmp"
export TEMP="$TMPDIR"
export TMP="$TMPDIR"
test -f ffmpeg-7.1/configure
test -f zlib-1.3.2/win32/Makefile.gcc
(
    cd zlib-1.3.2
    mingw32-make -f win32/Makefile.gcc -j4 libz.a \
        CC=x86_64-w64-mingw32-clang AR=llvm-ar
)
cd ffmpeg-7.1
bash ./configure \
    --target-os=mingw32 --arch=x86_64 --enable-cross-compile \
    --cc=x86_64-w64-mingw32-clang --cxx=x86_64-w64-mingw32-clang++ \
    --ar=llvm-ar --ranlib=llvm-ranlib --strip=llvm-strip \
    --disable-everything --disable-autodetect --disable-network \
    --disable-doc --disable-debug --disable-x86asm \
    --disable-ffplay --disable-ffprobe --disable-avdevice \
    --disable-postproc --disable-swresample \
    --enable-protocol=file --enable-demuxer=concat,image2,image_png_pipe \
    --enable-decoder=png --enable-parser=png --enable-encoder=png,gif \
    --enable-muxer=image2,gif \
    --enable-filter=scale,palettegen,paletteuse,split,format,null \
    --enable-zlib --extra-cflags=-I../zlib-1.3.2 \
    '--extra-ldflags=-L../zlib-1.3.2 -Wl,--no-insert-timestamp'
mingw32-make -j4 ffmpeg.exe SHELL=bash
./ffmpeg.exe -version
