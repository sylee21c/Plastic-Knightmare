import re
import sys
import zlib
from pathlib import Path


def decode_pdf_string(raw: bytes) -> str:
    if raw.startswith(b"\xfe\xff"):
        try:
            return raw[2:].decode("utf-16-be", errors="ignore")
        except UnicodeDecodeError:
            return ""

    for encoding in ("utf-8", "cp949", "latin-1"):
        try:
            return raw.decode(encoding, errors="ignore")
        except UnicodeDecodeError:
            pass
    return ""


def unescape_literal(value: bytes) -> bytes:
    value = value.replace(br"\(", b"(").replace(br"\)", b")")
    value = value.replace(br"\n", b"\n").replace(br"\r", b"\r").replace(br"\t", b"\t")
    value = value.replace(br"\\", b"\\")
    return value


def extract_strings(data: bytes) -> list[str]:
    strings: list[str] = []

    for match in re.finditer(rb"<([0-9A-Fa-f\s]+)>", data):
        hex_value = re.sub(rb"\s+", b"", match.group(1))
        if len(hex_value) < 4 or len(hex_value) % 2 != 0:
            continue
        try:
            strings.append(decode_pdf_string(bytes.fromhex(hex_value.decode("ascii"))))
        except ValueError:
            continue

    for match in re.finditer(rb"\((?:\\.|[^\\)])*\)", data, re.DOTALL):
        strings.append(decode_pdf_string(unescape_literal(match.group(0)[1:-1])))

    return [s.strip() for s in strings if s and s.strip()]


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    if len(sys.argv) != 2:
        print("Usage: ExtractPdfText.py <pdf>")
        return 2

    pdf = Path(sys.argv[1])
    data = pdf.read_bytes()
    chunks: list[bytes] = [data]

    for match in re.finditer(rb"stream\r?\n(.*?)\r?\nendstream", data, re.DOTALL):
        stream = match.group(1)
        try:
            chunks.append(zlib.decompress(stream))
        except zlib.error:
            pass

    text: list[str] = []
    for chunk in chunks:
        text.extend(extract_strings(chunk))

    seen: set[str] = set()
    for line in text:
        compact = re.sub(r"\s+", " ", line)
        if len(compact) < 2 or compact in seen:
            continue
        seen.add(compact)
        print(compact)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
