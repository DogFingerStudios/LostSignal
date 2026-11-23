"""Convert the aligned QGIS terrain GeoTIFF to the Unity game elevation asset."""
import argparse
from pathlib import Path

import numpy as np
from osgeo import gdal, osr


def export(source, destination):
    gdal.UseExceptions()
    expected = osr.SpatialReference()
    expected.ImportFromEPSG(32631)
    with gdal.Open(str(source)) as raster:
        if (raster.RasterXSize, raster.RasterYSize, raster.RasterCount) != (2000, 2000, 1):
            raise ValueError("Expected a single-band 2000 x 2000 terrain raster.")
        if raster.GetGeoTransform() != (0, 100, 0, 200000, 0, -100):
            raise ValueError("The raster must align to Yanorra's 100 m world grid.")
        crs = raster.GetSpatialRef()
        if crs is None or not crs.IsSame(expected):
            raise ValueError("Expected EPSG:32631.")
        band = raster.GetRasterBand(1)
        pixels = band.ReadAsArray().astype("<f4", copy=False)
        if not np.isfinite(pixels).all() or np.any(pixels == band.GetNoDataValue()):
            raise ValueError("The raster has missing heights. Keep elevation control points at all four corners.")
        band = None

    draft = destination.with_suffix(".tmp")
    try:
        pixels.tofile(draft)
        draft.replace(destination)
    finally:
        draft.unlink(missing_ok=True)
    print(f"Wrote {destination} ({pixels.min():.1f} to {pixels.max():.1f} m).")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    args = parser.parse_args()
    export(args.source, args.destination)
