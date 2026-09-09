# Hynnebergsbrottet — SMARCUnity world pack

AskoWorld layout: **Unity Terrain** for the lakebed, **one mesh per object**
(loading tower), **two lat/lon corners** so `GeoReferenceTransformer` can
place and scale the terrain, `GlobalReferencePoint` at the same origin as
`mesh/origin.json`.

This is **not** the 500 MB Poisson mesh. Overhangs do not belong on the
terrain; the tower is a separate object.

Playable scene: `Assets/Scenes/Hynnebergsbrottet.unity` (same folder as
AskoEmpty / Beckholmen). Bake assets live in `Assets/Scenes/Hynnebergsbrottet/`.

WMS tiles follow this scene’s `GlobalReferencePoint` lat/lon (Nora quarry),
not Askö. Do not edit `AskoEmpty` or the AskoWorld prefab.

## Import (automatic)

Open `smarc2/simulation/SMARCUnity` then `Assets/Scenes/Hynnebergsbrottet.unity`.
`HynnebergsbrottetApplyOnce` reapplies the RAW and builds `loading_tower`
from `Objects/loading_tower.bin` (no extra menu). Do not save onto AskoEmpty.

## Import (manual, same as Asko)

Duplicate
`Packages/com.smarc.assets/Runtime/Prefabs/Environment/GeoReferenced/AskoWorld`.

### 1. Terrain heightmap

Create a Terrain. **Import Raw**:

| Setting | Value |
|---|---|
| File | `Terrain/hynnebergsbrottet_heightmap.raw` |
| Depth | 16 bit |
| Resolution | 2049 × 2049 |
| Byte Order | Windows (little-endian) |
| Width | 545.650 m |
| Length | 347.100 m |
| Height | 33.909 m |

Terrain GameObject **position** (SW corner, heightmap 0 = deepest):

`(-268.9850, -33.9090, -168.9890)`

Waterline is world **Y = 0**. Heightmap 65535 = water.

### 2. GlobalReferencePoint

On `GLOBALREF` (exactly one in the scene):

- Origin Mode: Lat/Lon
- Unity frame: UTM
- Lat **59.10405185**  Lon **15.21461294**
- UTM 33V  E 512292.775  N 6551658.100

Unity position **(0, 0, 0)** — same origin as the old mesh tiles.

### 3. GeoRefTransformer (the two landmarks Ozer asked for)

Child of the Terrain. After placing, Inspector → **Transform to Match Earth Points**.

| Marker | Unity local (on terrain) | Lat | Lon |
|---|---|---|---|
| Unity SW | (0, 33.909, 0) | | |
| Unity NE | (545.650, 33.909, 347.100) | | |
| Unity Water Level | (272.825, 33.909, 173.550) | | |
| Earth SW | | 59.10254194 | 15.20990764 |
| Earth NE | | 59.10564329 | 15.21945326 |

`Terrain/hynnebergsbrottet_heightmap_preview.png` marks SW (green), NE (blue),
origin (yellow), and objects (red cross).

### 4. Loading tower

`loading_tower` is the **MeshLab ball-pivoting lattice** from the cleaned xyz
(`mesh/hynnebergsbrottet_tower_ml_bpa.ply`). Unity Y-up, **local to the
GameObject** (centroid). ApplyOnce builds it from `loading_tower.bin`
(~54 k verts / 100 k tris). Not a cuboid and not the 12 M-tri Poisson.

## Rebuild

```bash
python3 local/svp/build_unity_world.py
```

Source: class-0 `geotiff/depth_median_05cm.tif` for the terrain (no
overhangs). Tower pads from both classes of the combined cleaned xyz.
