# Third-party notices

## Velopack

https://github.com/velopack/velopack

MIT License

Copyright © 2021 Caelan Sayler

Copyright © 2024 Velopack Ltd.

Permission is hereby granted, free of charge, to any person obtaining a
copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.

## Bundled release tools

Release packages include pinned builds of the following command-line tools. Their license files are distributed in the `tools/licenses` directory.

- Node.js — https://nodejs.org/ — MIT and bundled third-party licenses
- yt-dlp — https://github.com/yt-dlp/yt-dlp — The Unlicense and bundled third-party licenses
- FFmpeg — https://ffmpeg.org/ — LGPLv3 build supplied by https://github.com/BtbN/FFmpeg-Builds

The exact versions, download locations, and SHA-256 checksums are recorded in `tools/toolchain.json` inside each release package and in `eng/toolchain.json` in the source repository.
The corresponding FFmpeg source revision is identified by the pinned build version and is available from https://github.com/FFmpeg/FFmpeg. Build scripts are available from https://github.com/BtbN/FFmpeg-Builds.
