import re
import sys
import zlib
from pathlib import Path


def main() -> int:
    if len(sys.argv) != 3:
        print("Usage: ExtractPdfImages.py <pdf> <output-dir>")
        return 2

    pdf = Path(sys.argv[1])
    output_dir = Path(sys.argv[2])
    output_dir.mkdir(parents=True, exist_ok=True)

    data = pdf.read_bytes()
    count = 0
    pattern = re.compile(rb"(?P<dict><<.*?/Subtype\s*/Image.*?>>)\s*stream\r?\n(?P<stream>.*?)\r?\nendstream", re.DOTALL)

    for match in pattern.finditer(data):
        dictionary = match.group("dict")
        width_match = re.search(rb"/Width\s+(\d+)", dictionary)
        height_match = re.search(rb"/Height\s+(\d+)", dictionary)
        bits_match = re.search(rb"/BitsPerComponent\s+(\d+)", dictionary)
        filter_match = re.search(rb"/Filter\s*/(\w+)", dictionary)

        if not width_match or not height_match or not bits_match:
            continue
        if bits_match.group(1) != b"8":
            continue

        width = int(width_match.group(1))
        height = int(height_match.group(1))
        stream = match.group("stream")

        if filter_match and filter_match.group(1) == b"FlateDecode":
            try:
                stream = zlib.decompress(stream)
            except zlib.error:
                continue
        elif filter_match:
            continue

        pixels = width * height
        if len(stream) >= pixels * 4:
            channels = 4
        elif len(stream) >= pixels * 3:
            channels = 3
        elif len(stream) >= pixels:
            channels = 1
        else:
            continue

        rgb = bytearray()
        for i in range(pixels):
            offset = i * channels
            if channels == 1:
                value = stream[offset]
                rgb.extend((value, value, value))
            else:
                rgb.extend(stream[offset:offset + 3])

        count += 1
        out = output_dir / f"pdf_image_{count:02d}.bmp"
        out.write_bytes(to_bmp(width, height, bytes(rgb)))
        print(out)

    return 0


def to_bmp(width: int, height: int, rgb: bytes) -> bytes:
    row_stride = ((width * 3 + 3) // 4) * 4
    pixel_size = row_stride * height
    file_size = 14 + 40 + pixel_size

    header = bytearray()
    header.extend(b"BM")
    header.extend(file_size.to_bytes(4, "little"))
    header.extend((0).to_bytes(4, "little"))
    header.extend((54).to_bytes(4, "little"))
    header.extend((40).to_bytes(4, "little"))
    header.extend(width.to_bytes(4, "little", signed=True))
    header.extend(height.to_bytes(4, "little", signed=True))
    header.extend((1).to_bytes(2, "little"))
    header.extend((24).to_bytes(2, "little"))
    header.extend((0).to_bytes(4, "little"))
    header.extend(pixel_size.to_bytes(4, "little"))
    header.extend((2835).to_bytes(4, "little"))
    header.extend((2835).to_bytes(4, "little"))
    header.extend((0).to_bytes(4, "little"))
    header.extend((0).to_bytes(4, "little"))

    pixels = bytearray()
    padding = b"\0" * (row_stride - width * 3)
    for y in range(height - 1, -1, -1):
        start = y * width * 3
        row = rgb[start:start + width * 3]
        for x in range(width):
            r, g, b = row[x * 3:x * 3 + 3]
            pixels.extend((b, g, r))
        pixels.extend(padding)

    return bytes(header + pixels)


if __name__ == "__main__":
    raise SystemExit(main())
