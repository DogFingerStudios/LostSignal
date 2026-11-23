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

After QGIS updates Elevation-from-points.tif, regenerate the Unity height
asset from the project root in PowerShell:

    & 'C:\Program Files\QGIS 3.44.7\bin\python-qgis.bat' 'Docs\ExportUnityElevation.py' 'Maps\Elevation-from-points.tif' 'Assets\MapData\elevation.bytes'

Use the aligned Float32 raster with no gaps. Unity reads it as meter
elevations on the same grid. Save QGIS edits and let the terrain macro
finish before running the export. Enter Play Mode again after replacing
the inputs. Seeded height, tree, and water generation is disabled for
streamed tiles. Scene-authored terrain, trees, and props remain in place.
