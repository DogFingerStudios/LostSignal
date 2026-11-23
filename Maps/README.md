# Lost Signal — Yanorra map

Open `LostSignal-Map.qgz` in QGIS 3.44.7. Keep the project, `LostSignal.gpkg`,
`Spawn-point.gpkg`, and `Elevation.tif` together. The project uses relative paths. Zoom to
**World boundary** to return to the playable area.

## Coordinates

The project and all nine layers use **EPSG:32631, WGS 84 / UTM zone 31N**.
`Yanorra-CRS.wkt` contains the exact WKT2:2019 definition. The projection
uses Transverse Mercator with latitude of origin 0°, central meridian 3° E,
scale factor 0.9996, false easting 500,000 m, and false northing 0 m. The
Earth datum and projection parameters are a technical convention. Treat
the plane as Yanorra's local grid, with origin at the playable southwest
corner and no conversion to Earth longitude or latitude.

Playable bounds are (0, 0) to (200000, 200000). Grid east is Unity X,
grid north is Unity Z, and elevation in meters is Unity Y. One Unity unit
represents one meter. The initial view adds 5 km on each side before QGIS
fits it to the canvas.

## Drawing

Select an editable vector layer in the Layers
panel, turn on editing, and use the layer's Add Feature tool. Save edits
to the GeoPackage when finished. QGIS assigns an immutable-in-the-form
text UUID to each new feature in these layers. Keep that text unchanged
when updating or exporting a feature. GeoPackage's internal `fid` is a
separate row key. The World boundary is a reference polygon.

- Road and site vertices snap within 12 screen pixels. A route can be
  drawn as one line across multiple future Unity chunks.
- For biomes, a blank `vegetation_density` leaves the biome default in
  effect. Use 1.0 for normal density and 0 for no procedural vegetation.
- For sites, `heading_deg` is measured clockwise from grid north.
  Use 0 for north and 90 for east, with values in [0, 360).
- Save a site before selecting its name in the clearing's `site_id` field.
  The field stores the site's UUID. Use the polygon footprint to exclude
  procedural vegetation in Unity.

## Setting the player spawn

Open the **Sites** group and select **Spawn Point**. Turn on editing and
add a point where the player should spawn. The default name is Player
spawn. Set the heading in degrees clockwise from north, with 0 for north
and 90 for east. The gold star marks the point on the map.

With project macros enabled, saving edits to Spawn Point, Sites, or Site
clearings refreshes the corresponding spawn_point.geojson, sites.geojson,
or site_clearings.geojson beside the project.

## Drawing water

Use **Rivers and streams** for waterway centerlines. Choose river, stream,
or seasonal stream. The optional `width_m` stores full channel width in
meters. Vertices snap within 12 screen pixels. Use **Water bodies** for
shoreline polygons, with lake, pond, or reservoir as the type. Both layers
store a stable UUID, name, type, and notes.

## Drawing elevation control points

Select **Elevation control points**, turn on editing, and use **Add Point
Feature**. Enter the elevation in meters in **Height (m)** and save the
edits. Include points at all four corners for full coverage, plus points
at terrain features such as summits, saddles, and valley floors.

In the Processing Toolbox, run **TIN interpolation** with **Elevation
control points** as a Points input and **Height (m)** (`height_m`) as the
interpolation attribute. Choose **Linear**, use **World boundary** for
the extent, and set both pixel sizes to 100 m. Export to a separate
GeoTIFF and inspect it before replacing the placeholder. Rerun the tool
after changing control points. The points are terrain authoring inputs
and can be kept in the GeoPackage. Unity receives the generated raster.

## Replacing the elevation raster

`Elevation.tif` is one Float32 band in meters, initialized to 0 m.
For a drop-in replacement, resample to 2000 columns by 2000 rows in
EPSG:32631 and align to outer bounds xmin=0, ymin=0, xmax=200000,
ymax=200000. The north-up geotransform is `(0, 100, 0, 200000, 0, -100)`.
The first pixel center is (50, 199950). Store actual elevations in meters
and preserve valid zero elevations. Close the project before replacing the
file at the same relative path. Reopen it and adjust the elevation display
range if needed. The 100 m cell size is for regional design.

## GeoJSON handoff to Unity

This is a **project-specific GeoJSON convention**. Coordinates are
Yanorra grid meters in [east, north] order. Standard RFC 7946 GeoJSON
uses WGS 84 longitude and latitude and is unsuitable for this handoff.
Unity should consume the coordinate numbers directly as X and Z.

Enable the project macros when opening the project. Saving layer edits
automatically creates or replaces that layer's GeoJSON file beside the
project. The macro exports the entire saved layer, including deletions.
The automated layers include Elevation control points. The control-point export is elevation_control_points.geojson and includes height_m in meters. The interpolated surface is Elevation-from-points.tif.

To export manually, save edits first. In QGIS, right-click each authored layer and choose
**Export > Save Features As…**. Repeat once per layer using
`roads_and_trails.geojson`, `biomes.geojson`, `sites.geojson`, and
`site_clearings.geojson`. Also export `rivers_and_streams.geojson` and `water_bodies.geojson`. If Unity also needs the reference polygon,
export it with the same settings as `world_boundary.geojson`.

- Format **GeoJSON**, encoding **UTF-8**, CRS **EPSG:32631** (same as layer).
- Under layer options, set **RFC7946 = NO** and
  **COORDINATE_PRECISION = 8**.
- Export all features and select every attribute except the provider
  `fid`, if present. Keep original field names and raw attribute values.
  In particular, export `site_id` as its stored UUID, rather than as the
  site name shown in the form. Disable replacement with displayed values
  and field aliases if offered.
- Preserve the layer's geometry type and omit Z. Unity gets elevations
  from the raster and uses `properties.id` as the stable vector ID.
