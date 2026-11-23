import csv
import datetime
import re
import sys

import openpyxl

workbook = openpyxl.load_workbook(sys.argv[1], read_only=True, data_only=True)
offset = datetime.timedelta(hours=workbook["Juhend"]["B13"].value)

servers = {}
for row in workbook["Serverid"].iter_rows(min_row=2, values_only=True):
    if row[0]:
        servers[row[0]] = row[7]

rows = []
for row in workbook["Vaatlused"].iter_rows(min_row=2, values_only=True):
    nr = row[0]
    checked_local = row[3]
    note = row[7] or ""
    if not nr:
        continue

    match = re.match(r"Ring (\d+):", note)
    if not match:
        continue

    found = checked_local is not None
    checked = None
    if found:
        checked = (checked_local - offset).replace(tzinfo=datetime.timezone.utc)

    wipes = []
    if "tulevased (UTC):" in note:
        listed = note.split("tulevased (UTC):")[-1]
        for kind, day, month, hour, minute in re.findall(r"(kaart \+ BP|kaart|BP)\s+(\d\d)\.(\d\d) (\d\d):(\d\d)", listed):
            if kind == "BP":
                continue
            year = checked.year
            if int(month) < checked.month:
                year = year + 1
            wipes.append(datetime.datetime(year, int(month), int(day), int(hour), int(minute), tzinfo=datetime.timezone.utc))

    next_wipe = None
    for wipe in sorted(wipes):
        if wipe > checked:
            next_wipe = wipe
            break

    rows.append([
        servers[nr],
        match.group(1),
        checked.isoformat() if checked else "",
        1 if found else 0,
        next_wipe.isoformat() if next_wipe else "",
    ])

with open(sys.argv[2], "w", newline="", encoding="utf-8") as f:
    writer = csv.writer(f)
    writer.writerow(["server_id", "round", "checked_utc", "found", "next_map_wipe_utc"])
    writer.writerows(rows)

found_count = 0
next_count = 0
for row in rows:
    found_count += row[3]
    if row[4]:
        next_count += 1
print(len(rows), "rows,", found_count, "found,", next_count, "with a next map wipe")
