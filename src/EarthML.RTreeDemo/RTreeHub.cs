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
using EarthML.GeoJson;
using EarthML.GeoJson.Geometries;
using EarthML.SpatialIndex.RTree;
using EarthML.SpatialIndex.RTree.FileSystem;
using EarthML.SpatialIndex.RTree.GeoJson;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


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

        
        public async Task<GeoJsonFeatureCollection> Search(GeoJsonFeature feature)
        {

         //   var geom = feature.SelectToken("geometry");

            var geometry = GetGeom(feature.Geometry);      
            return new GeoJsonFeatureCollection
            {
                Features = Grid.Value.Search(feature).Select(d => d.Data)
                    .Where(result => geometry.Intersects(GetGeom(result.Geometry)))
                    .ToArray()
            };

        
        }

        private IGeometry GetGeom(GeometryObject geometry)
        {
            if (geometry is GeoJson.Geometries.Polygon)
            {
                var coords = geometry as GeoJson.Geometries.Polygon;
                var linearRings = coords.Coordinates.Select(rings => new LinearRing(rings.Select(c => new Coordinate(c))));

                var poly = new DotSpatial.Topology.Polygon(linearRings.First(), linearRings.Skip(1).ToArray());
                return poly;
            }
            else if (geometry is GeoJson.Geometries.Point)
            {
                return new DotSpatial.Topology.Point(new Coordinate(
                    (geometry as GeoJson.Geometries.Point).Coordinates));
            }

            throw new NotImplementedException();
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
