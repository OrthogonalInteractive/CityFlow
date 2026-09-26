"""Extract the Tokyo Station inspection subset without importing source data into Unity."""

import argparse
import hashlib
import json
from pathlib import Path
import shutil
from zipfile import ZipFile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path)
    parser.add_argument("--output", type=Path, default=Path(".local-data/plateau/chiyoda-2025-tokyo-station"))
    args = parser.parse_args()
    destination = args.output.resolve()
    tiles = ("53394611", "53394621")
    prefixes = tuple(f"udx/{kind}/{tile}" for kind in ("bldg", "tran", "brid", "veg") for tile in tiles)
    with ZipFile(args.archive) as archive:
        selected = [entry for entry in archive.infolist() if not entry.is_dir() and (
            entry.filename.startswith(prefixes + ("codelists/", "schemas/", "metadata/"))
            or entry.filename in ("README.md", "udx/dem/533946_dem_6697_op.gml"))]
        if not any(entry.filename == "udx/bldg/53394611_bldg_6697_op.gml" for entry in selected):
            raise ValueError("The archive does not contain the expected Tokyo Station building mesh.")
        for entry in selected:
            output = (destination / entry.filename).resolve()
            if not output.is_relative_to(destination):
                raise ValueError(f"Unsafe archive path: {entry.filename}")
            output.parent.mkdir(parents=True, exist_ok=True)
            with archive.open(entry) as source, output.open("wb") as target:
                shutil.copyfileobj(source, target)
        digest = hashlib.sha256()
        with args.archive.open("rb") as source:
            for block in iter(lambda: source.read(8 * 1024 * 1024), b""):
                digest.update(block)
        report = {
            "archive": args.archive.name,
            "archive_sha256": digest.hexdigest(),
            "mesh_codes": tiles,
            "files": len(selected),
            "expanded_bytes": sum(entry.file_size for entry in selected),
            "output": str(destination),
        }
        (destination / "extraction.json").write_text(json.dumps(report, indent=2) + "\n")
        print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
