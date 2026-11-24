# Yanorra map data in Unity

Keep spawn_point.geojson and sites.geojson in Assets/MapData. Unity imports
the QGIS files directly. Set exactly one spawn point and give each Horizon
Chain node a unique id and a name. The KeepDriving scene already references
the data files and SatelliteDish prefab. Update the files in place so their
Unity asset references stay intact.

The spawn point selects the logical starting tile and the player's position
within the terrain already in the scene. That terrain and its hand-placed
objects keep their scene coordinates. The streamed terrain uses a common
vertical offset to meet the authored terrain at the spawn, and blends to
the authored height along the starting tile edges. The grid is 500 m per
tile and the streamer loads at most nine terrain tiles. Map-created dishes
are kept independently of terrain for the current play session.

Keep elevation_control_points.geojson in Assets/MapData. Unity reads the
saved height_m values at startup and interpolates between the points
using a triangulated surface. Keep a point at each world corner to
control heights along the edges. When a corner is missing, Unity samples
that corner from elevation.bytes, or uses 0 m if that asset is absent.

Replace the GeoJSON in place and reenter Play Mode to see your edits.
Rebuild a standalone game to include the updated asset. The QGIS
GeoTIFF is useful for previewing terrain in QGIS. Unity can still use
elevation.bytes in scenes without a control-point asset.

Scene-authored terrain, trees, and props remain in place. Seeded height,
tree, and water generation is disabled for streamed tiles.
