"""Check reviewed translations and report remaining five-language documentation work.

Use --require-complete for the full-repository completion gate. A successful default
run verifies only the documents recorded in the manifest; it does not claim that
all repository documentation is translated or prove semantic accuracy by itself.
"""

import argparse
from collections import Counter
from decimal import Decimal
import hashlib
import json
from pathlib import Path
import re
from urllib.parse import unquote, urlsplit, urlunsplit


ROOT = Path(__file__).resolve().parents[1]
LANGUAGES = ("zh-CN", "zh-TW", "en", "ja", "tr")
SUFFIX = re.compile(r"\.(?:zh-CN|zh-TW|en|ja|tr)(?=\.md$)")
LINK = re.compile(r"\[[^\]]*\]\(([^)]+)\)")


def canonical(path: str) -> str:
    parsed = urlsplit(path)
    return urlunsplit(parsed._replace(path=SUFFIX.sub("", parsed.path)))


def structure(text: str, json_text_fields: tuple[str, ...] = (), mermaid_labels: tuple[str, ...] = ()) -> tuple:
    return (
        Counter(len(match) for match in re.findall(r"^(#{1,6})\s", text, re.M)),
        len(re.findall(r"^- ", text, re.M)),
        re.findall(r"^([0-9]+)\. ", text, re.M),
        Counter(re.findall(r"(?<!`)`([^`\n]+)`(?!`)", text)),
        command_blocks(text, json_text_fields, mermaid_labels),
        len(re.split(r"\n\s*\n", text.strip())),
    )


def command_blocks(text: str, json_text_fields: tuple[str, ...] = (), mermaid_labels: tuple[str, ...] = ()) -> list[tuple[str, int]]:
    result = []
    found_labels = set()
    for language, body in re.findall(r"^```([^\n]*)\n(.*?)^```", text, re.M | re.S):
        lines = body.splitlines(keepends=True)
        # Full-line shell/Python comments are explanatory prose and must be
        # translated too. Their number and all executable lines stay unchanged.
        if language.strip() in ("powershell", "sh", "bash", "python"):
            comments = [line for line in lines if re.match(r"^\s*#(?!\!)", line)]
            result.append(("".join(line for line in lines if line not in comments), len(comments)))
        elif language.strip() == "mermaid" and mermaid_labels:
            def label(match):
                node, text = match.groups()
                if node not in mermaid_labels:
                    return match.group()
                if not text.strip():
                    raise ValueError(f"Empty translated Mermaid label: {node}")
                found_labels.add(node)
                # Visible wording may change; node IDs, edges, syntax, and digits stay exact.
                digits = re.findall(r"\d+(?:\.\d+)*", text)
                return node + "[<translated label; digits=" + json.dumps(digits) + ">]"
            result.append((re.sub(r"([A-Za-z_]\w*)\[([^\]\n]*)\]", label, body), 0))
        elif language.strip() == "json" and json_text_fields:
            # A reviewed preset example may translate only its human-facing text.
            # Decimal parsing preserves every digit in technical numeric values.
            data = json.loads(body, parse_float=Decimal, parse_int=Decimal,
                              object_pairs_hook=unique_json_fields, parse_constant=reject_json_constant)
            if not isinstance(data, dict) or not isinstance(data.get("id"), str):
                raise ValueError("Translated JSON text is only allowed in preset objects with an id")
            for field in json_text_fields:
                if field not in data:
                    continue
                if not isinstance(data[field], str) or not data[field].strip():
                    raise ValueError(f"Empty or invalid translated preset field: {field}")
                data[field] = f"<translated preset {field}>"
            result.append((json.dumps(typed_json(data), ensure_ascii=False), 0))
        else:
            result.append((body, 0))
    if found_labels != set(mermaid_labels):
        raise ValueError("Declared translated Mermaid labels were not found")
    return result


def parallel_language_view(text: str, labels: tuple[str, ...], require_pairs: bool = False) -> str:
    """Read each change once while preserving bilingual release-publishing sources.

    Only explicitly declared, paired H3 bullet lists inside an H2 release may
    collapse. Both lists must preserve item counts and technical inline tokens.
    Ordinary release subsections and all other content remain in the inventory.
    """
    if not labels:
        return text
    if len(labels) != 2 or len(set(labels)) != 2 or any(not x.strip() or "\n" in x for x in labels):
        raise ValueError("Parallel language sections require two distinct heading labels")
    result = []
    pairs = 0
    for section in re.split(r"(?=^## )", text, flags=re.M):
        headings = list(re.finditer(r"^### ([^\n]+)\n", section, re.M))
        if not any(heading[1] in labels for heading in headings):
            result.append(section)
            continue
        if [heading[1] for heading in headings] != list(labels):
            raise ValueError("Incomplete or unexpected parallel release sections")
        bodies = [section[heading.end():headings[i + 1].start() if i + 1 < len(headings) else len(section)].strip()
                  for i, heading in enumerate(headings)]
        for body in bodies:
            if not body or any(line.strip() and not line.startswith("- ") for line in body.splitlines()):
                raise ValueError("Parallel release sections must contain only complete bullet lists")
        if structure(bodies[0])[:5] != structure(bodies[1])[:5]:
            raise ValueError("Parallel release items or technical tokens differ")
        result.append(section[:headings[0].start()] + bodies[0] + "\n\n")
        pairs += 1
    if require_pairs and not pairs:
        raise ValueError("Declared parallel source sections were not found")
    # Blank lines between single-line release bullets are formatting, not extra facts.
    return re.sub(r"\n+(?=- )", "\n\n", "".join(result)).strip() + "\n"


def unique_json_fields(pairs: list[tuple[str, object]]) -> dict:
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON field in translated example: {key}")
        result[key] = value
    return result


def reject_json_constant(value: str):
    raise ValueError(f"Invalid JSON number in translated example: {value}")


def typed_json(value: object) -> list:
    if isinstance(value, dict):
        return ["object", [[key, typed_json(child)] for key, child in sorted(value.items())]]
    if isinstance(value, list):
        return ["array", [typed_json(child) for child in value]]
    if isinstance(value, Decimal):
        return ["number", str(value)]
    if isinstance(value, str):
        return ["string", value]
    return ["null"] if value is None else ["boolean", value]


def tables(text: str) -> list[list[list[str]]]:
    result = []
    current = []
    for line in text.splitlines() + [""]:
        if line.lstrip().startswith("|"):
            # Pipes inside inline code are data, rather than column separators.
            cells, cell, in_code = [], "", False
            for character in line.strip().strip("|"):
                if character == "`":
                    in_code = not in_code
                if character == "|" and not in_code and not cell.endswith("\\"):
                    cells.append(cell.strip())
                    cell = ""
                else:
                    cell += character
            cells.append(cell.strip())
            current.append(cells)
        elif current:
            result.append(current)
            current = []
    return result


def numeric_values(cell: str, language: str) -> list[Decimal]:
    values = re.findall(r"[+−-]?\d+(?:[.,]\d+)*", cell)
    return [Decimal((value.replace(".", "").replace(",", ".") if language == "tr"
                     else value.replace(",", "")).replace("−", "-")) for value in values]


def documentation_sources(root: Path) -> set[str]:
    """Inventory repository prose, excluding generated build and test output."""
    return {
        path.relative_to(root).as_posix()
        for path in root.rglob("*.md")
        if path.relative_to(root).parts[0] not in ("artifacts", "tmp")
        and not any(part in (".git", "bin", "obj", ".venv", "__pycache__")
                    for part in path.relative_to(root).parts)
        and not SUFFIX.search(path.name)
    }


def generated_reference_paths(root: Path, source: Path, text: str, declared: list[str]) -> set[str]:
    """Allow only explicitly documented outputs beneath the ignored artifacts tree."""
    links = set(LINK.findall(text))
    if len(declared) != len(set(declared)):
        raise ValueError("Duplicate generated artifact declaration")
    for reference in declared:
        parsed = urlsplit(reference)
        target = (source.parent / unquote(parsed.path)).resolve()
        if (reference not in links or parsed.scheme or parsed.netloc or parsed.query or parsed.fragment
                or Path(parsed.path).is_absolute() or not target.is_relative_to((root / "artifacts").resolve())):
            raise ValueError(f"Invalid generated artifact reference: {reference}")
    return set(declared)


def check(require_complete: bool = False) -> None:
    manifest = json.loads((ROOT / "docs/translations.json").read_text(encoding="utf-8"))
    reviewed = set()
    for entry in manifest["documents"]:
        source = ROOT / entry["source"]
        source_text = source.read_text(encoding="utf-8")
        if hashlib.sha256(source_text.encode("utf-8")).hexdigest() != entry["source_sha256"]:
            raise ValueError(f"Source changed; review its translations: {entry['source']}")
        if set(entry["translations"]) != set(LANGUAGES):
            raise ValueError(f"Incomplete declared language set: {entry['source']}")
        json_text_fields = tuple(entry.get("translated_json_text_fields", ()))
        if not set(json_text_fields) <= {"displayName", "description"}:
            raise ValueError(f"Unsupported translated JSON fields: {entry['source']}")
        mermaid_labels = tuple(entry.get("translated_mermaid_labels", ()))
        if (len(mermaid_labels) != len(set(mermaid_labels))
                or any(not re.fullmatch(r"[A-Za-z_]\w*", node) for node in mermaid_labels)):
            raise ValueError(f"Invalid translated Mermaid node IDs: {entry['source']}")
        parallel_labels = tuple(entry.get("parallel_language_sections", ()))
        source_text = parallel_language_view(source_text, parallel_labels, require_pairs=bool(parallel_labels))
        expected_structure = structure(source_text, json_text_fields, mermaid_labels)
        source_tables = tables(source_text)
        source_links = Counter(canonical(link) for link in LINK.findall(source_text))
        generated = generated_reference_paths(ROOT, source, source_text, entry.get("generated_artifacts", []))
        for language, relative in entry["translations"].items():
            path = ROOT / relative
            text = parallel_language_view(path.read_text(encoding="utf-8"), parallel_labels)
            if structure(text, json_text_fields, mermaid_labels) != expected_structure:
                raise ValueError(f"Changed sections, paragraphs, list items, inline code, or command blocks: {relative}")
            translated_tables = tables(text)
            if [[len(row) for row in table] for table in translated_tables] != [[len(row) for row in table] for table in source_tables]:
                raise ValueError(f"Changed table rows or columns: {relative}")
            for source_table, target_table in zip(source_tables, translated_tables):
                for source_row, target_row in zip(source_table, target_table):
                    for source_cell, target_cell in zip(source_row, target_row):
                        # Identical literals include clause numbers such as 10.7.1.2,
                        # which are technical references rather than decimal values.
                        if source_cell != target_cell and re.fullmatch(r"[\d\s.,%/=+×()−–-]+", source_cell) and re.search(r"\d", source_cell):
                            if numeric_values(source_cell, entry["source_language"]) != numeric_values(target_cell, language):
                                raise ValueError(f"Changed numeric table data in {relative}: {source_cell} -> {target_cell}")
            if Counter(canonical(link) for link in LINK.findall(text)) != source_links:
                raise ValueError(f"Changed reference targets: {relative}")
            for link in LINK.findall(text):
                parsed = urlsplit(link)
                if (not parsed.scheme and parsed.path and link not in generated
                        and not (path.parent / unquote(parsed.path)).exists()):
                    raise ValueError(f"Broken local reference in {relative}: {link}")
            if not any(f"]({Path(target).name})" in text for target in entry["translations"].values()):
                raise ValueError(f"Language navigation missing: {relative}/{language}")
        reviewed.add(entry["source"])

    sources = documentation_sources(ROOT)
    remaining = sorted(sources - reviewed)
    print(f"Documentation: {len(reviewed)}/{len(sources)} source documents recorded with five-language parity.")
    print("Recorded translations passed section, paragraph, list, table-data, command, reference, and source-version checks.")
    if remaining:
        print("Full documentation parity remains incomplete:")
        for path in remaining:
            print("  " + path)
        if require_complete:
            raise SystemExit(1)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--require-complete", action="store_true")
    check(parser.parse_args().require_complete)
