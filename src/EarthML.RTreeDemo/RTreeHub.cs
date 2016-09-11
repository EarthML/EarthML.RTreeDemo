using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Threading;
using System.Threading.Tasks;
using DotSpatial.Topology;
using EarthML.SpatialIndex.RTree;
using EarthML.SpatialIndex.RTree.FileSystem;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SInnovations.VectorTiles.GeoJsonVT.GeoJson;
using SInnovations.VectorTiles.GeoJsonVT.GeoJson.Geometries;
using SInnovations.VectorTiles.GeoJsonVT.Processing;

namespace EarthML.RTreeDemo
{

    public class RTreeHub : Hub
    {
        public static Dictionary<string, RTree<JObject>> Trees = new Dictionary<string, RTree<JObject>>();
        public static Lazy<RTree<GeoJsonFeature>> Grid = new Lazy<RTree<GeoJsonFeature>>(CreateTree);

        private static RTree<GeoJsonFeature> CreateTree()
        {


            var tree = new RTree<GeoJsonFeature>(100);

            tree.UseArhiveStore("data/tree.zip");


            return tree;
        }



        public override Task OnConnected()
        {
            var tree = new RTree<JObject>(9);
            Trees.Add(this.Context.ConnectionId, tree);


            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            Trees.Remove(this.Context.ConnectionId);
            return base.OnDisconnected(stopCalled);
        }

        public async Task SendGrid()
        {



            //     SendTree(Grid.Value.root,false);
        }
        public async Task<GeoJsonFeatureCollection> Search(JObject feature)
        {

            var geom = feature.SelectToken("geometry");

            var geometry = Unpack(feature.SelectToken("geometry"));

            var mbr = new SpatialIndex.RTree.Envelope(
             geometry.Coordinates.Min(c => c.X) / 360.0 + 0.5,
             geometry.Coordinates.Min(c => c.Y) / 180 + 0.5,
               geometry.Coordinates.Max(c => c.X) / 360.0 + 0.5,
               geometry.Coordinates.Max(c => c.Y) / 180 + 0.5);

            System.GC.Collect();

            return new GeoJsonFeatureCollection
            {
                Features = Grid.Value.Search(mbr).Select(d => d.Data)
                    .Where(result => geometry.Intersects(GetGeom(result.Geometry)))
                    .ToArray()
            };

        
        }

        private IGeometry GetGeom(GeometryObject geometry)
        {
            if (geometry is SInnovations.VectorTiles.GeoJsonVT.GeoJson.Geometries.Polygon)
            {
                var coords = geometry as SInnovations.VectorTiles.GeoJsonVT.GeoJson.Geometries.Polygon;
                var linearRings = coords.Coordinates.Select(rings => new LinearRing(rings.Select(c => new Coordinate(c))));

                var poly = new DotSpatial.Topology.Polygon(linearRings.First(), linearRings.Skip(1).ToArray());
                return poly;
            }
            else if (geometry is SInnovations.VectorTiles.GeoJsonVT.GeoJson.Geometries.Point)
            {
                return new DotSpatial.Topology.Point(new Coordinate(
                    (geometry as SInnovations.VectorTiles.GeoJsonVT.GeoJson.Geometries.Point).Coordinates));
            }

            throw new NotImplementedException();
        }

        public async Task AddFeature(JObject feature)
        {
            Console.WriteLine(feature.ToString(Newtonsoft.Json.Formatting.Indented));


            //  double[] x = null; double[] y = null;


            var geometry = Unpack(feature.SelectToken("geometry"));

            Trees[Context.ConnectionId].Insert(feature, new SpatialIndex.RTree.Envelope(
             geometry.Coordinates.Min(c => c.X) / 360.0 + 0.5,
             geometry.Coordinates.Min(c => c.Y) / 180 + 0.5,
               geometry.Coordinates.Max(c => c.X) / 360.0 + 0.5,
               geometry.Coordinates.Max(c => c.Y) / 180 + 0.5));

            var node = Trees[Context.ConnectionId].root;

            Clients.Caller.ClearTree();
            SendTree(node);

        }

        private static Geometry Unpack(JToken geom)
        {
            switch (geom.SelectToken("type").ToString())
            {
                case "Point":
                    //   x = geom.SelectToken("coordinates").ToObject<double[]>().Where((p, i) => i % 2 == 0).ToArray();
                    //   y = geom.SelectToken("coordinates").ToObject<double[]>().Where((p, i) => i % 2 == 1).ToArray();
                    return new DotSpatial.Topology.Point(new Coordinate(geom.SelectToken("coordinates").ToObject<double[]>()));

                case "Polygon":
                    //  x = geom.SelectToken("coordinates").ToObject<double[][][]>().SelectMany(p => p.SelectMany(p1 => p1)).Where((p, i) => i % 2 == 0).ToArray();
                    //  y = geom.SelectToken("coordinates").ToObject<double[][][]>().SelectMany(p => p.SelectMany(p1 => p1)).Where((p, i) => i % 2 == 1).ToArray();
                    var linearRings = geom.SelectToken("coordinates").ToObject<double[][][]>()
                        .Select(rings => new LinearRing(rings.Select(c => new Coordinate(c))));

                    var poly = new DotSpatial.Topology.Polygon(linearRings.First(), linearRings.Skip(1).ToArray());
                    return poly;


                default:
                    throw new NotImplementedException();
            }
        }

        private void SendTree<T>(RTreeNode<T> node, bool includedata = false)
        {
            var shouldShow = true;
            if (!includedata)
            {
                if (node.Data != null)
                    shouldShow = false;
            }
            if (shouldShow)
            {
                Clients.Caller.UpdateTree(new JObject(
                    new JProperty("id", node.Id),
                     new JProperty("height", node.Height),
                     new JProperty("data", node.Data),
                    new JProperty("geometry",
                    new JObject(
                        new JProperty("type", "Polygon"),
                        new JProperty("coordinates", JToken.FromObject(new double[][][] {
                        new double[][]
                        {
                            new double[] {((node.Envelope.X1)-0.5)*360, (node.Envelope.Y1-0.5)*180},
                            new double[] {((node.Envelope.X1)-0.5) * 360, (node.Envelope.Y2-0.5)*180 },
                            new double[] {((node.Envelope.X2) - 0.5)*360, (node.Envelope.Y2-0.5)*180 },
                            new double[] {((node.Envelope.X2) - 0.5)*360, (node.Envelope.Y1-0.5)*180 },
                            new double[] {((node.Envelope.X1) - 0.5)*360, (node.Envelope.Y1 - 0.5) * 180 }


                        }
                        })

                    ))
                    )));
            }

            foreach (var child in node.Children)
            {
                SendTree(child, includedata);
            }
        }
    }
}
