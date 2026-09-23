"""Generate the small, fictional Lục Hải map used by the Phase 1 prototype."""

import json
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
MAP_WIDTH = 1600
MAP_HEIGHT = 1000
SEA_COLOR = (21, 47, 55)

# The hand-shaped outer coast and seed points define a small fictional region.
COAST = [
    (192, 480), (255, 355), (350, 260), (470, 205), (610, 170),
    (775, 150), (940, 165), (1095, 200), (1230, 260), (1345, 350),
    (1395, 445), (1385, 535), (1325, 625), (1230, 700), (1100, 755),
    (960, 790), (810, 810), (660, 790), (525, 795), (395, 760),
    (295, 695), (230, 610),
]

PROVINCES = [
    (1, "Cồn Nguyệt", 1, "Highlands", 470, 265, 162000, 1.08, 1.12, 0.13),
    (2, "Thượng Vân", 1, "Forest", 670, 245, 184000, 1.12, 1.18, 0.12),
    (3, "Bắc Đài", 2, "Highlands", 870, 255, 148000, 0.98, 1.21, 0.14),
    (4, "Lâm Uyên", 2, "Forest", 1080, 265, 176000, 1.04, 1.16, 0.12),
    (5, "Mũi Vạc", 3, "Coast", 1240, 340, 131000, 1.02, 1.08, 0.15),
    (6, "Cửa Sương", 1, "Coast", 320, 385, 209000, 1.16, 1.09, 0.14),
    (7, "Bình Lưu", 1, "Plains", 505, 415, 246000, 1.22, 1.17, 0.12),
    (8, "Cao Dã", 2, "Highlands", 705, 400, 192000, 1.03, 1.24, 0.11),
    (9, "Hỏa Nguyên", 2, "Plains", 910, 410, 227000, 1.18, 1.21, 0.13),
    (10, "Đồng Muối", 3, "Coast", 1110, 435, 157000, 1.09, 1.11, 0.16),
    (11, "Hải Môn", 3, "Coast", 1290, 485, 221000, 1.21, 1.08, 0.15),
    (12, "Cánh Sậy", 1, "Marsh", 350, 545, 173000, 0.96, 0.98, 0.11),
    (13, "Trung Châu", 2, "Plains", 570, 550, 281000, 1.29, 1.25, 0.12),
    (14, "Bến Trầm", 4, "Marsh", 770, 565, 136000, 0.93, 1.02, 0.10),
    (15, "Suối Nắng", 3, "Plains", 980, 570, 195000, 1.15, 1.19, 0.14),
    (16, "Cảng Vàng", 3, "Coast", 1170, 580, 268000, 1.34, 1.27, 0.16),
    (17, "Trúc Am", 4, "Forest", 450, 690, 119000, 0.91, 1.05, 0.10),
    (18, "Đồng Sáng", 4, "Plains", 665, 700, 234000, 1.18, 1.16, 0.13),
    (19, "Bãi Hổ Phách", 4, "Coast", 875, 700, 188000, 1.08, 1.13, 0.15),
    (20, "Lưỡi Thủy Triều", 3, "Coast", 1070, 690, 207000, 1.24, 1.10, 0.15),
]

COUNTRIES = [
    {"countryId": 1, "name": "An Lưu", "mapColor": "#C96E55", "isAiControlled": False, "capitalProvinceId": 7, "treasury": 8000},
    {"countryId": 2, "name": "Dạ Lam", "mapColor": "#5D9C90", "isAiControlled": True, "capitalProvinceId": 9, "treasury": 6500},
    {"countryId": 3, "name": "Minh Sa", "mapColor": "#7E88B5", "isAiControlled": True, "capitalProvinceId": 16, "treasury": 7000},
    {"countryId": 4, "name": "Tùng Hải", "mapColor": "#C3A451", "isAiControlled": True, "capitalProvinceId": 18, "treasury": 5500},
]

GAME_SETTINGS = {"startDate": "1444-01-01", "startingSpeed": 1}


def clip_half_plane(polygon, nx, ny, limit):
    """Clip a convex polygon to nx*x + ny*y <= limit."""
    if not polygon:
        return []

    result = []
    previous = polygon[-1]
    previous_value = nx * previous[0] + ny * previous[1] - limit

    for current in polygon:
        current_value = nx * current[0] + ny * current[1] - limit
        previous_inside = previous_value <= 1e-7
        current_inside = current_value <= 1e-7

        if previous_inside != current_inside:
            amount = previous_value / (previous_value - current_value)
            result.append((
                previous[0] + amount * (current[0] - previous[0]),
                previous[1] + amount * (current[1] - previous[1]),
            ))

        if current_inside:
            result.append(current)

        previous = current
        previous_value = current_value

    return result


def voronoi_cell(site, all_sites):
    polygon = list(COAST)
    for index, first in enumerate(COAST):
        second = COAST[(index + 1) % len(COAST)]
        edge_x = second[0] - first[0]
        edge_y = second[1] - first[1]
        # COAST runs counter-clockwise, so its interior is left of every edge.
        polygon = clip_half_plane(polygon, edge_y, -edge_x, edge_y * first[0] - edge_x * first[1])

    site_x, site_y = site
    for other_x, other_y in all_sites:
        if (other_x, other_y) == site:
            continue
        normal_x = 2 * (other_x - site_x)
        normal_y = 2 * (other_y - site_y)
        limit = (other_x * other_x + other_y * other_y) - (site_x * site_x + site_y * site_y)
        polygon = clip_half_plane(polygon, normal_x, normal_y, limit)
    return polygon


def edge_key(first, second):
    first_point = (round(first[0], 2), round(first[1], 2))
    second_point = (round(second[0], 2), round(second[1], 2))
    return tuple(sorted((first_point, second_point)))


def main():
    output_root = Path(__file__).resolve().parents[1]
    data_dir = output_root / "Data"
    map_dir = output_root / "assets" / "maps"
    data_dir.mkdir(parents=True, exist_ok=True)
    map_dir.mkdir(parents=True, exist_ok=True)

    sites = [(province[4], province[5]) for province in PROVINCES]
    polygons = [voronoi_cell(site, sites) for site in sites]
    if any(len(polygon) < 3 for polygon in polygons):
        raise RuntimeError("A generated province has fewer than three map points.")

    edge_owners = {}
    for province_index, polygon in enumerate(polygons):
        for index, first in enumerate(polygon):
            second = polygon[(index + 1) % len(polygon)]
            key = edge_key(first, second)
            if key[0] == key[1]:
                continue
            owners = edge_owners.setdefault(key, [])
            province_id = province_index + 1
            if province_id not in owners:
                owners.append(province_id)

    connections = {province[0]: set() for province in PROVINCES}
    coastal_ids = set()
    for owners in edge_owners.values():
        if len(owners) == 1:
            coastal_ids.add(owners[0])
        elif len(owners) == 2:
            first, second = owners
            connections[first].add(second)
            connections[second].add(first)
        else:
            raise RuntimeError(f"Unexpected number of provinces sharing an edge: {owners}")

    province_rows = []
    for province, polygon in zip(PROVINCES, polygons):
        province_id, name, owner_id, terrain, x, y, population, economy, development, tax_rate = province
        province_rows.append({
            "provinceId": province_id,
            "name": name,
            "ownerCountryId": owner_id,
            "controllerCountryId": owner_id,
            "population": population,
            "economy": economy,
            "development": development,
            "taxRate": tax_rate,
            "manpower": int(population * 0.075),
            "terrain": terrain,
            "isCoastal": province_id in coastal_ids,
            "capitalPositionX": x,
            "capitalPositionY": y,
            "vertices": [[round(point[0], 3), round(point[1], 3)] for point in polygon],
        })

    write_json(data_dir / "countries.json", COUNTRIES)
    write_json(data_dir / "provinces.json", province_rows)
    write_json(data_dir / "game_settings.json", GAME_SETTINGS)
    write_json(data_dir / "province_connections.json", {
        str(province_id): sorted(neighbors) for province_id, neighbors in connections.items()
    })

    id_map = Image.new("RGB", (MAP_WIDTH, MAP_HEIGHT), SEA_COLOR)
    draw = ImageDraw.Draw(id_map)
    for province, polygon in zip(PROVINCES, polygons):
        province_id = province[0]
        draw.polygon(polygon, fill=(0, 0, province_id))
    id_map.save(map_dir / "province_id_map.png", optimize=True)

    print(f"Generated {len(province_rows)} provinces, {len(COUNTRIES)} countries, and {len(connections)} graph nodes.")


def write_json(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
