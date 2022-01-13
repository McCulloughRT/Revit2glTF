﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;

namespace glTFRevitExport
{
    public class glTFExportConfigs {
        /// <summary>
        /// Flag to export all buffers into a single .bin file (if true).
        /// </summary>
        public bool SingleBinary = true;
        
        /// <summary>
        /// Flag to export as single .glb file (if true).
        /// </summary>
        public bool AsGLB = true;

        /// <summary>
        /// Flag to export all the properties for each element.
        /// </summary>
        public bool ExportProperties = true;

        /// <summary>
        /// Flag to write coords as Z up instead of Y up (if true).
        /// </summary>
        public bool FlipCoords = true;

        /// <summary>
        /// Include non-standard elements that are not part of
        /// official glTF spec. If false, non-standard elements will be excluded
        /// </summary>
        public bool IncludeNonStdElements = true;
    }

    public class glTFExportContext : IExportContext
    {
        private glTFExportConfigs _cfgs = new glTFExportConfigs();

        /// <summary>
        /// The name for the export files
        /// </summary>
        private string _filename;
        
        /// <summary>
        /// The directory for the export files
        /// </summary>
        private string _directory;

        private bool _skipElementFlag = false;

        private GLTFManager manager = new GLTFManager();
        private Stack<Document> documentStack = new Stack<Document>();
        private Document _doc
        {
            get
            {
                return documentStack.Peek();
            }
        }

        public glTFExportContext(Document doc, string filename, string directory, glTFExportConfigs configs = null)
        {
            documentStack.Push(doc);

            // ensure filename is really a file name and no extension
            _filename = Path.GetFileNameWithoutExtension(filename);
            _directory = directory;
            _cfgs = configs is null ? _cfgs : configs;
        }

        /// <summary>
        /// Runs once at beginning of export. Sets up the root node
        /// and scene.
        /// </summary>
        /// <returns></returns>
        public bool Start()
        {
            Debug.WriteLine("Starting...");
            manager.Start(_cfgs.ExportProperties);
            return true;
        }

        /// <summary>
        /// Runs once at end of export. Serializes the gltf
        /// properties and wites out the *.gltf and *.bin files.
        /// </summary>
        public void Finish()
        {
            Debug.WriteLine("Finishing...");

            glTFContainer container = manager.Finish();

            if (_cfgs.IncludeNonStdElements) {
                // TODO: [RM] Standardize what non glTF spec elements will go into
                // this "BIM glTF superset" and write a spec for it. Gridlines below
                // are an example.

                // Add gridlines as gltf nodes in the format:
                // Origin {Vec3<double>}, Direction {Vec3<double>}, Length {double}
                FilteredElementCollector col = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Grid));

                var grids = col.ToElements();
                foreach (Grid g in grids) {
                    Line l = g.Curve as Line;

                    var origin = l.Origin;
                    var direction = l.Direction;
                    var length = l.Length;

                    var xtras = new glTFExtras();
                    var grid = new GridParameters();
                    grid.origin = new List<double>() { origin.X, origin.Y, origin.Z };
                    grid.direction = new List<double>() { direction.X, direction.Y, direction.Z };
                    grid.length = length;
                    xtras.GridParameters = grid;
                    xtras.UniqueId = g.UniqueId;
                    xtras.Properties = Util.GetElementProperties(g, true);

                    var gridNode = new glTFNode();
                    gridNode.name = g.Name;
                    gridNode.extras = xtras;

                container.glTF.nodes.Add(gridNode);
                container.glTF.nodes[0].children.Add(container.glTF.nodes.Count - 1);
                }
            }

            if (_cfgs.SingleBinary || _cfgs.AsGLB)
            {
                int bytePosition = 0;
                int currentBuffer = 0;
                foreach (var view in container.glTF.bufferViews)
                {
                    if (view.buffer == 0)
                    {
                        bytePosition += view.byteLength;
                        continue;
                    }

                    if (view.buffer != currentBuffer)
                    {
                        view.buffer = 0;
                        view.byteOffset = bytePosition;
                        bytePosition += view.byteLength;
                    }
                }

                glTFBuffer buffer = new glTFBuffer();
                var binaryFileName = _filename + ".bin";
                buffer.uri = _cfgs.AsGLB ? null : binaryFileName;
                buffer.byteLength = bytePosition;
                container.glTF.buffers.Clear();
                container.glTF.buffers.Add(buffer);

                if (_cfgs.AsGLB)
                {
                    byte[] byteBuffer;  
                    MemoryStream memoryStream = new MemoryStream();
                    using (BinaryWriter writer = new BinaryWriter(memoryStream))
                    {
                        foreach (var bin in container.binaries)
                        {
                            bin.WriteData(writer);
                        }
                        byteBuffer = memoryStream.ToArray();
                    }
                    var gltf = JsonConvert.SerializeObject(container.glTF,
                                                                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore,
                                                                    Formatting = Formatting.None});

                    using (var f = File.Create(Path.Combine(_directory, _filename + ".glb")))
                    {
                        using (var writer = new BinaryWriter(f))
                        {
                            SaveBinaryModel(gltf,byteBuffer,writer);
                        }
                    }
                    return;
                }

                using (FileStream f = File.Create(Path.Combine(_directory, buffer.uri)))
                {
                    using (BinaryWriter writer = new BinaryWriter(f))
                    {
                        foreach (var bin in container.binaries)
                        {
                            bin.WriteData(writer);
                        }
                    }
                }

            }
            else
            {
                // Write the *.bin files
                foreach (var bin in container.binaries)
                {
                    using (FileStream f = File.Create(Path.Combine(_directory, bin.name)))
                    {
                        using (BinaryWriter writer = new BinaryWriter(f))
                        {
                            bin.WriteData(writer);
                        }
                    }
                }
            }

            // Write the *.gltf file
            string serializedModel = JsonConvert.SerializeObject(container.glTF, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            File.WriteAllText(Path.Combine(_directory, _filename + ".gltf"), serializedModel);
        }

       

        public void SaveBinaryModel(string serializedModel, byte[] buffer, BinaryWriter binaryWriter)
        {
            const uint GLTFHEADER = 0x46546C67;
            const uint GLTFVERSION2 = 2;
            const uint CHUNKJSON = 0x4E4F534A;
            const uint CHUNKBIN = 0x004E4942;
            
            var jsonText = serializedModel;
            var jsonChunk = Encoding.UTF8.GetBytes(jsonText);
            var jsonPadding = jsonChunk.Length & 3; if (jsonPadding != 0) jsonPadding = 4 - jsonPadding;

            if (buffer != null && buffer.Length == 0) buffer = null;
            var binPadding = buffer == null ? 0 : buffer.Length & 3; if (binPadding != 0) binPadding = 4 - binPadding;

            int fullLength = 4 + 4 + 4;

            fullLength += 8 + jsonChunk.Length + jsonPadding;
            if (buffer != null) fullLength += 8 + buffer.Length + binPadding;

            binaryWriter.Write(GLTFHEADER);
            binaryWriter.Write(GLTFVERSION2);
            binaryWriter.Write(fullLength);

            binaryWriter.Write(jsonChunk.Length + jsonPadding);
            binaryWriter.Write(CHUNKJSON);
            binaryWriter.Write(jsonChunk);
            for (int i = 0; i < jsonPadding; ++i) binaryWriter.Write((Byte)0x20);

            if (buffer != null)
            {
                binaryWriter.Write(buffer.Length + binPadding);
                binaryWriter.Write(CHUNKBIN);
                binaryWriter.Write(buffer);
                for (int i = 0; i < binPadding; ++i) binaryWriter.Write((Byte)0);
            }
        }

        /// <summary>
        /// Runs once for each element.
        /// </summary>
        /// <param name="elementId">ElementId of Element being processed</param>
        /// <returns></returns>
        public RenderNodeAction OnElementBegin(ElementId elementId)
        {
            Element e = _doc.GetElement(elementId);
            Debug.WriteLine(String.Format("{2}OnElementBegin: {1}-{0}", e.Name, elementId, manager.formatDebugHeirarchy));

            if (manager.containsNode(e.UniqueId))
            {
                // Duplicate element, skip adding.
                Debug.WriteLine(String.Format("{0}  Duplicate Element!", manager.formatDebugHeirarchy));
                _skipElementFlag = true;
                return RenderNodeAction.Skip;
            }

            manager.OpenNode(e);

            return RenderNodeAction.Proceed;
        }

        /// <summary>
        /// Runs every time, and immediately prior to, a mesh being processed (OnPolymesh).
        /// It supplies the material for the mesh, and we use this to create a new material
        /// in our material container, or switch the current material if it already exists.
        /// TODO: Handle more complex materials.
        /// </summary>
        /// <param name="node"></param>
        public void OnMaterial(MaterialNode matNode)
        {
            Debug.WriteLine(String.Format("{0}  OnMaterial", manager.formatDebugHeirarchy));
            string matName;
            string uniqueId;

            ElementId id = matNode.MaterialId;
            if (id != ElementId.InvalidElementId)
            {
                Element m = _doc.GetElement(matNode.MaterialId);
                matName = m.Name;
                uniqueId = m.UniqueId;
            }
            else
            {
                uniqueId = string.Format("r{0}g{1}b{2}", matNode.Color.Red.ToString(), matNode.Color.Green.ToString(), matNode.Color.Blue.ToString());
                matName = string.Format("MaterialNode_{0}_{1}", Util.ColorToInt(matNode.Color), Util.RealString(matNode.Transparency * 100));
            }

            Debug.WriteLine(String.Format("{1}  Material: {0}", matName, manager.formatDebugHeirarchy));
            manager.SwitchMaterial(matNode, matName, uniqueId);
        }

        /// <summary>
        /// Runs for every polymesh being processed. Typically this is a single face
        /// of an element's mesh. Vertices and faces are keyed on the element/material combination 
        /// (this is important because within a single element, materials can be changed and 
        /// repeated in unknown order).
        /// </summary>
        /// <param name="polymesh"></param>
        public void OnPolymesh(PolymeshTopology polymesh)
        {
            Debug.WriteLine(String.Format("{0}  OnPolymesh", manager.formatDebugHeirarchy));
            manager.OnGeometry(polymesh);
        }

        /// <summary>
        /// Runs at the end of an element being processed, after all other calls for that element.
        /// </summary>
        /// <param name="elementId"></param>
        public void OnElementEnd(ElementId elementId)
        {
            Debug.WriteLine(String.Format("{0}OnElementEnd", manager.formatDebugHeirarchy.Substring(0, manager.formatDebugHeirarchy.Count() - 2)));
            if (_skipElementFlag)
            {
                _skipElementFlag = false;
                return;
            }

            manager.CloseNode();
        }

        /// <summary>
        /// This is called when family instances are encountered, after OnElementBegin.
        /// We're using it here to maintain the transform stack for that element's heirarchy.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public RenderNodeAction OnInstanceBegin(InstanceNode node)
        {
            Debug.WriteLine(String.Format("{0}OnInstanceBegin", manager.formatDebugHeirarchy));
            
            ElementId symId = node.GetSymbolId();
            Element symElem = _doc.GetElement(symId);

            Debug.WriteLine(String.Format("{2}OnInstanceBegin: {0}-{1}", symId, symElem.Name, manager.formatDebugHeirarchy));

            var nodeXform = node.GetTransform();
            manager.OpenNode(symElem, nodeXform.IsIdentity ? null : nodeXform, true);

            return RenderNodeAction.Proceed;
        }

        /// <summary>
        /// This is called when family instances are encountered, before OnElementEnd.
        /// We're using it here to maintain the transform stack for that element's heirarchy.
        /// </summary>
        /// <param name="node"></param>
        public void OnInstanceEnd(InstanceNode node)
        {
            Debug.WriteLine(String.Format("{0}OnInstanceEnd", manager.formatDebugHeirarchy.Substring(0,manager.formatDebugHeirarchy.Count() - 2)));

            ElementId symId = node.GetSymbolId();
            Element symElem = _doc.GetElement(symId);

            manager.CloseNode(symElem, true);
        }

        public bool IsCanceled()
        {
            // This method is invoked many times during the export process.
            return false;
        }

        public RenderNodeAction OnViewBegin(ViewNode node)
        {
            // TODO: we could use this to handle multiple scenes in the gltf file.
            return RenderNodeAction.Proceed;
        }

        public void OnViewEnd(ElementId elementId)
        {
            // do nothing
        }

        public RenderNodeAction OnLinkBegin(LinkNode node)
        {
            ElementId symId = node.GetSymbolId();
            Element symElem = _doc.GetElement(symId);

            Debug.WriteLine(String.Format("{2}OnLinkBegin: {0}-{1}", symId, symElem.Name, manager.formatDebugHeirarchy));

            var nodeXform = node.GetTransform();
            manager.OpenNode(symElem, nodeXform.IsIdentity ? null : nodeXform, true);

            documentStack.Push(node.GetDocument());
            return RenderNodeAction.Proceed;
        }

        public void OnLinkEnd(LinkNode node)
        {
            Debug.WriteLine(String.Format("{0}OnLinkEnd", manager.formatDebugHeirarchy.Substring(0, manager.formatDebugHeirarchy.Count() - 2)));
            manager.CloseNode();

            documentStack.Pop();
        }

        public RenderNodeAction OnFaceBegin(FaceNode node)
        {
            return RenderNodeAction.Proceed;
        }

        public void OnFaceEnd(FaceNode node)
        {
            // This method is invoked only if the 
            // custom exporter was set to include faces.
        }

        public void OnRPC(RPCNode node)
        {
            // do nothing
        }

        public void OnLight(LightNode node)
        {
            // do nothing
        }
    }
}
