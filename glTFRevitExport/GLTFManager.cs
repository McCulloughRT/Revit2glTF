using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace glTFRevitExport
{
    static class ManagerUtils
    {
        static public List<double> ConvertXForm(Transform xform)
        {
            if (xform.IsIdentity) return null;

            var BasisX = xform.BasisX;
            var BasisY = xform.BasisY;
            var BasisZ = xform.BasisZ;
            var Origin = xform.Origin;
            var OriginX = PointInt.ConvertFeetToMillimetres(Origin.X);
            var OriginY = PointInt.ConvertFeetToMillimetres(Origin.Y);
            var OriginZ = PointInt.ConvertFeetToMillimetres(Origin.Z);

            List<double> glXform = new List<double>(16) {
                BasisX.X, BasisX.Y, BasisX.Z, 0,
                BasisY.X, BasisY.Y, BasisY.Z, 0,
                BasisZ.X, BasisZ.Y, BasisZ.Z, 0,
                OriginX, OriginY, OriginZ, 1
            };

            return glXform;
        }

        public class HashSearch
        {
            string _S;
            public HashSearch(string s)
            {
                _S = s;
            }
            public bool EqualTo(HashedType d)
            {
                return d.hashcode.Equals(_S);
            }
        }

        static public string GenerateSHA256Hash<T>(T data)
        {
            var binFormatter = new BinaryFormatter();
            var mStream = new MemoryStream();
            binFormatter.Serialize(mStream, data);

            using (SHA256 hasher = SHA256.Create())
            {
                mStream.Position = 0;
                byte[] byteHash = hasher.ComputeHash(mStream);

                var sBuilder = new StringBuilder();
                for (int i = 0; i < byteHash.Length; i++)
                {
                    sBuilder.Append(byteHash[i].ToString("x2"));
                }

                return sBuilder.ToString();
            }
        }
    }

    class GLTFManager
    {
        private IndexedDictionary<glTFMaterial> materialDict = new IndexedDictionary<glTFMaterial>();
        private Dictionary<string, Node> nodeDict = new Dictionary<string, Node>();
        private List<MeshContainer> meshContainers = new List<MeshContainer>();

        public List<glTFScene> scenes = new List<glTFScene>();
        public List<glTFBuffer> buffers = new List<glTFBuffer>();
        public List<glTFBufferView> bufferViews = new List<glTFBufferView>();
        public List<glTFAccessor> accessors = new List<glTFAccessor>();
        public List<glTFBinaryData> binaryFileData = new List<glTFBinaryData>();

        public List<glTFNode> nodes {
            get {
                var list = nodeDict.Values.ToList();
                return list.OrderBy(x => x.index).Select(x => x.ToGLTFNode()).ToList();
            }
        }

        public List<glTFMaterial> materials {
            get {
                return materialDict.List;
            }
        }

        public List<glTFMesh> meshes {
            get {
                return meshContainers.Select(x => x.contents).ToList();
            }
        }

        private bool _flipCoords = false;
        private bool _exportProperties = true;

        private string currentNodeId;
        private Dictionary<string, GeometryData> currentGeometry;
        private Stack<string> parentStack = new Stack<string>();

        public void Start(bool exportProperties = true)
        {
            this._exportProperties = exportProperties;

            Node rootNode = new Node(0);
            rootNode.children = new List<int>();
            nodeDict.Add(rootNode.id, rootNode);

            glTFScene defaultScene = new glTFScene();
            defaultScene.nodes.Add(0);
            scenes.Add(defaultScene);
        }

        public glTFContainer Finish()
        {
            glTF model = new glTF();
            model.asset = new glTFVersion();
            model.scenes = scenes;
            model.nodes = nodes;
            model.meshes = meshes;
            model.materials = materials;
            model.buffers = buffers;
            model.bufferViews = bufferViews;
            model.accessors = accessors;

            glTFContainer container = new glTFContainer();
            container.glTF = model;
            container.binaries = binaryFileData;

            return container;
        }

        public void openNode(Element elem, Transform xform = null)
        {
            if (nodeDict.ContainsKey(elem.UniqueId)) return;

            Node node = new Node(elem, nodeDict.Count, _exportProperties);
            if (parentStack.Count > 0)
            {
                string parentId = parentStack.Peek();
                Node parentNode = nodeDict[parentId];
                if (parentNode.children == null) parentNode.children = new List<int>();
                nodeDict[parentId].children.Add(node.index);
            }

            parentStack.Push(node.id);
            if (xform != null)
            {
                node.matrix = ManagerUtils.ConvertXForm(xform);
            }

            nodeDict.Add(node.id, node);
            currentNodeId = node.id;

            // TODO: do geometry initialization
            openGeometry();
        }

        public void closeNode(Element elem)
        {
            Node node = nodeDict[elem.UniqueId];
            node.isFinalized = true;
            currentNodeId = null;

            // do geometry finalization
            closeGeometry();

            // retreat back up parent tree
            parentStack.Pop();
        }

        public void switchMaterial(MaterialNode matNode, string name = null, string id = null)
        {
            glTFMaterial gl_mat = new glTFMaterial();
            string uniqueId = id;
            if (uniqueId == null)
            {
                uniqueId = string.Format("r{0}g{1}b{2}", matNode.Color.Red.ToString(), matNode.Color.Green.ToString(), matNode.Color.Blue.ToString());
                gl_mat.name = string.Format("MaterialNode_{0}_{1}", Util.ColorToInt(matNode.Color), Util.RealString(matNode.Transparency * 100));

                glTFPBR pbr = new glTFPBR();
                pbr.baseColorFactor = new List<float>() { matNode.Color.Red / 255f, matNode.Color.Green / 255f, matNode.Color.Blue / 255f, (float)matNode.Transparency * 255 };
                pbr.metallicFactor = 0f;
                pbr.roughnessFactor = 1f;
                gl_mat.pbrMetallicRoughness = pbr;
            } else
            {
                gl_mat.name = name;
                glTFPBR pbr = new glTFPBR();
                pbr.baseColorFactor = new List<float>() { matNode.Color.Red / 255f, matNode.Color.Green / 255f, matNode.Color.Blue / 255f, (float)matNode.Transparency * 255 };
                pbr.metallicFactor = 0f;
                pbr.roughnessFactor = 1f;
                gl_mat.pbrMetallicRoughness = pbr;
            }

            materialDict.AddOrUpdateCurrent(uniqueId, gl_mat);
        }

        public void openGeometry()
        {
            currentGeometry = new Dictionary<string, GeometryData>();
        }

        public void onGeometry(PolymeshTopology polymesh)
        {
            if (currentNodeId == null) throw new Exception();
            string vertex_key = currentNodeId + "_" + materialDict.CurrentKey;
            if (currentGeometry.ContainsKey(vertex_key) == false)
            {
                currentGeometry.Add(vertex_key, new GeometryData());
            }

            // Populate normals from this polymesh
            IList<XYZ> norms = polymesh.GetNormals();
            foreach (XYZ norm in norms)
            {
                currentGeometry[vertex_key].normals.Add(norm.X);
                currentGeometry[vertex_key].normals.Add(norm.Y);
                currentGeometry[vertex_key].normals.Add(norm.Z);
            }

            // Populate vertex and faces data
            IList<XYZ> pts = polymesh.GetPoints();
            foreach (PolymeshFacet facet in polymesh.GetFacets())
            {
                int v1 = currentGeometry[vertex_key].vertDictionary.AddVertex(new PointInt(pts[facet.V1], _flipCoords));
                int v2 = currentGeometry[vertex_key].vertDictionary.AddVertex(new PointInt(pts[facet.V2], _flipCoords));
                int v3 = currentGeometry[vertex_key].vertDictionary.AddVertex(new PointInt(pts[facet.V3], _flipCoords));

                currentGeometry[vertex_key].faces.Add(v1);
                currentGeometry[vertex_key].faces.Add(v2);
                currentGeometry[vertex_key].faces.Add(v3);
            }
        }

        public void closeGeometry()
        {
            // Create the new mesh and populate the primitives with GeometryData
            glTFMesh mesh = new glTFMesh();
            mesh.primitives = new List<glTFMeshPrimitive>();

            // transfer ordered vertices from vertex dictionary to vertices list
            foreach (KeyValuePair<string,GeometryData> key_geom in currentGeometry)
            {
                string key = key_geom.Key;
                GeometryData geom = key_geom.Value;
                foreach (KeyValuePair<PointInt, int> point_index in geom.vertDictionary)
                {
                    PointInt point = point_index.Key;
                    geom.vertices.Add(point.X);
                    geom.vertices.Add(point.Y);
                    geom.vertices.Add(point.Z);
                }

                // convert GeometryData objects into glTFMeshPrimitive
                string material_key = key.Split('_')[1];
                glTFBinaryData bufferMeta = processGeometry(geom, key);
                glTFMeshPrimitive primative = new glTFMeshPrimitive();

                primative.attributes.POSITION = bufferMeta.vertexAccessorIndex;
                primative.indices = bufferMeta.indexAccessorIndex;
                primative.material = materialDict.GetIndexFromUUID(material_key);
                // TODO: Add normal attribute accessor index here

                mesh.primitives.Add(primative);
            }

            // Prevent mesh duplication by hash checking
            string meshHash = ManagerUtils.GenerateSHA256Hash(mesh);
            ManagerUtils.HashSearch hs = new ManagerUtils.HashSearch(meshHash);
            int idx = meshContainers.FindIndex(hs.EqualTo);

            if (idx != -1)
            {
                // set the current nodes mesh index to the already
                // created mesh location.
                nodeDict[currentNodeId].mesh = idx;
            }
            else
            {
                // create new mesh and add it's index to the current node.
                MeshContainer mc = new MeshContainer();
                mc.hashcode = meshHash;
                mc.contents = mesh;
                meshContainers.Add(mc);
                nodeDict[currentNodeId].mesh = meshContainers.Count - 1;
            }

            // Clear the geometry
            currentGeometry = null;
            return;
        }

        private glTFBinaryData processGeometry(GeometryData geom, string name)
        {
            // TODO: rename this type to glTFBufferMeta ?
            glTFBinaryData bufferData = new glTFBinaryData();
            glTFBinaryBufferContents bufferContents = new glTFBinaryBufferContents();

            foreach (var coord in geom.vertices)
            {
                float vFloat = Convert.ToSingle(coord);
                bufferContents.vertexBuffer.Add(vFloat);
            }
            foreach (var index in geom.faces)
            {
                bufferContents.indexBuffer.Add(index);
            }

            // Prevent buffer duplication by hash checking
            string calculatedHash = ManagerUtils.GenerateSHA256Hash(bufferContents);
            ManagerUtils.HashSearch hs = new ManagerUtils.HashSearch(calculatedHash);
            var match = binaryFileData.Find(hs.EqualTo);

            if (match != null)
            {
                // return previously created buffer metadata
                bufferData.vertexAccessorIndex = match.vertexAccessorIndex;
                bufferData.indexAccessorIndex = match.indexAccessorIndex;
                return bufferData;
            }
            else
            {
                // add a buffer
                glTFBuffer buffer = new glTFBuffer();
                buffer.uri = name + ".bin";
                buffers.Add(buffer);
                int bufferIdx = buffers.Count - 1;

                /**
                 * Buffer Data
                 **/
                bufferData.name = buffer.uri;
                bufferData.contents = bufferContents;
                // TODO: Uncomment for normals
                //foreach (var normal in geomData.normals)
                //{
                //    bufferData.normalBuffer.Add((float)normal);
                //}

                // Get max and min for vertex data
                float[] vertexMinMax = Util.GetVec3MinMax(bufferContents.vertexBuffer);
                // Get max and min for index data
                int[] faceMinMax = Util.GetScalarMinMax(bufferContents.indexBuffer);
                // TODO: Uncomment for normals
                // Get max and min for normal data
                //float[] normalMinMax = getVec3MinMax(bufferData.normalBuffer);

                /**
                 * BufferViews
                 **/
                // Add a vec3 buffer view
                int elementsPerVertex = 3;
                int bytesPerElement = 4;
                int bytesPerVertex = elementsPerVertex * bytesPerElement;
                int numVec3 = (geom.vertices.Count) / elementsPerVertex;
                int sizeOfVec3View = numVec3 * bytesPerVertex;
                glTFBufferView vec3View = new glTFBufferView();
                vec3View.buffer = bufferIdx;
                vec3View.byteOffset = 0;
                vec3View.byteLength = sizeOfVec3View;
                vec3View.target = Targets.ARRAY_BUFFER;
                bufferViews.Add(vec3View);
                int vec3ViewIdx = bufferViews.Count - 1;

                // TODO: Add a normals (vec3) buffer view

                // Add a faces / indexes buffer view
                int elementsPerIndex = 1;
                int bytesPerIndexElement = 4;
                int bytesPerIndex = elementsPerIndex * bytesPerIndexElement;
                int numIndexes = geom.faces.Count;
                int sizeOfIndexView = numIndexes * bytesPerIndex;
                glTFBufferView facesView = new glTFBufferView();
                facesView.buffer = bufferIdx;
                facesView.byteOffset = vec3View.byteLength;
                facesView.byteLength = sizeOfIndexView;
                facesView.target = Targets.ELEMENT_ARRAY_BUFFER;
                bufferViews.Add(facesView);
                int facesViewIdx = bufferViews.Count - 1;

                buffers[bufferIdx].byteLength = vec3View.byteLength + facesView.byteLength;

                /**
                 * Accessors
                 **/
                // add a position accessor
                glTFAccessor positionAccessor = new glTFAccessor();
                positionAccessor.bufferView = vec3ViewIdx;
                positionAccessor.byteOffset = 0;
                positionAccessor.componentType = ComponentType.FLOAT;
                positionAccessor.count = geom.vertices.Count / elementsPerVertex;
                positionAccessor.type = "VEC3";
                positionAccessor.max = new List<float>() { vertexMinMax[1], vertexMinMax[3], vertexMinMax[5] };
                positionAccessor.min = new List<float>() { vertexMinMax[0], vertexMinMax[2], vertexMinMax[4] };
                accessors.Add(positionAccessor);
                bufferData.vertexAccessorIndex = accessors.Count - 1;

                // TODO: Uncomment for normals
                // add a normals accessor
                //glTFAccessor normalsAccessor = new glTFAccessor();
                //normalsAccessor.bufferView = vec3ViewIdx;
                //normalsAccessor.byteOffset = (positionAccessor.count) * bytesPerVertex;
                //normalsAccessor.componentType = ComponentType.FLOAT;
                //normalsAccessor.count = geom.data.normals.Count / elementsPerVertex;
                //normalsAccessor.type = "VEC3";
                //normalsAccessor.max = new List<float>() { normalMinMax[1], normalMinMax[3], normalMinMax[5] };
                //normalsAccessor.min = new List<float>() { normalMinMax[0], normalMinMax[2], normalMinMax[4] };
                //this.accessors.Add(normalsAccessor);
                //bufferData.normalsAccessorIndex = this.accessors.Count - 1;

                // add a face accessor
                glTFAccessor faceAccessor = new glTFAccessor();
                faceAccessor.bufferView = facesViewIdx;
                faceAccessor.byteOffset = 0;
                faceAccessor.componentType = ComponentType.UNSIGNED_INT;
                faceAccessor.count = numIndexes;
                faceAccessor.type = "SCALAR";
                faceAccessor.max = new List<float>() { faceMinMax[1] };
                faceAccessor.min = new List<float>() { faceMinMax[0] };
                accessors.Add(faceAccessor);
                bufferData.indexAccessorIndex = accessors.Count - 1;

                bufferData.hashcode = calculatedHash;

                return bufferData;
            }
        }
    }

    class Node : glTFNode
    {
        public int index;
        public string id;
        public bool isFinalized = false;

        public Node(Element elem, int index, bool exportProperties = true)
        {
            this.name = Util.ElementDescription(elem);
            this.id = elem.UniqueId;
            this.index = index;

            if (exportProperties)
            {
                // get the extras for this element
                glTFExtras extras = new glTFExtras();
                extras.UniqueId = elem.UniqueId;
                extras.Properties = Util.GetElementProperties(elem, true);
                this.extras = extras;
            }
        }
        public Node(int index)
        {
            this.name = "::rootNode::";
            this.id = System.Guid.NewGuid().ToString();
            this.index = index;
        }
        public Node(glTFNode node)
        {
            this.name = node.name;
            this.mesh = node.mesh;
            this.matrix = node.matrix;
            this.extras = node.extras;
            this.children = node.children;
        }

        public glTFNode ToGLTFNode()
        {
            glTFNode node = new glTFNode();
            node.name = this.name;
            node.mesh = this.mesh;
            node.matrix = this.matrix;
            node.extras = this.extras;
            node.children = this.children;
            return node;
        }
    }
}
