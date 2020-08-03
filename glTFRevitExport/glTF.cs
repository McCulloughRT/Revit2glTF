using System;
using System.Collections.Generic;

namespace glTFRevitExport
{
    /// <summary>
    /// Magic numbers to differentiate scalar and vector 
    /// array buffers.
    /// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#buffers-and-buffer-views
    /// </summary>
    public enum Targets
    {
        ARRAY_BUFFER = 34962, // signals vertex data
        ELEMENT_ARRAY_BUFFER = 34963 // signals index or face data
    }

    /// <summary>
    /// Magic numbers to differentiate array buffer component
    /// types.
    /// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#accessor-element-size
    /// </summary>
    public enum ComponentType
    {
        BYTE = 5120,
        UNSIGNED_BYTE = 5121,
        SHORT = 5122,
        UNSIGNED_SHORT = 5123,
        UNSIGNED_INT = 5125,
        FLOAT = 5126
    }

    public struct glTFContainer
    {
        public glTF glTF;
        public List<glTFBinaryData> binaries;
    }

    /// <summary>
    /// The json serializable glTF file format.
    /// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0
    /// </summary>
    public struct glTF
    {
        public glTFVersion asset;
        public List<glTFScene> scenes;
        public List<glTFNode> nodes;
        public List<glTFMesh> meshes;
        public List<glTFBuffer> buffers;
        public List<glTFBufferView> bufferViews;
        public List<glTFAccessor> accessors;
        public List<glTFMaterial> materials;
    }

    /// <summary>
    /// A binary data store serialized to a *.bin file
    /// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#binary-data-storage
    /// </summary>
    public class glTFBinaryData : HashedType
    {
        public glTFBinaryBufferContents contents { get; set; }
        //public List<float> vertexBuffer { get; set; } = new List<float>();
        //public List<int> indexBuffer { get; set; } = new List<int>();
        //public List<float> normalBuffer { get; set; } = new List<float>();
        public int vertexAccessorIndex { get; set; }
        public int indexAccessorIndex { get; set; }
        //public int normalsAccessorIndex { get; set; }
        public string name { get; set; }
        //public string hashcode { get; set; }
    }

    [Serializable]
    public class glTFBinaryBufferContents
    {
        public List<float> vertexBuffer { get; set; } = new List<float>();
        public List<int> indexBuffer { get; set; } = new List<int>();
    }

    /// <summary>
    /// Required glTF asset information
    /// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#asset
    /// </summary>
    public class glTFVersion
    {
        public string version = "2.0";
    }

    /// <summary>
    /// The scenes available to render.
    /// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#scenes
    /// </summary>
    public class glTFScene
    {
        public List<int> nodes = new List<int>();
    }

    /// <summary>
    /// The nodes defining individual (or nested) elements in the scene.
    /// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#nodes-and-hierarchy
    /// </summary>
    public class glTFNode
    {
        /// <summary>
        /// The user-defined name of this object
        /// </summary>
        public string name { get; set; }
        /// <summary>
        /// The index of the mesh in this node.
        /// </summary>
        public int? mesh { get; set; } = null;
        /// <summary>
        /// A floating-point 4x4 transformation matrix stored in column major order.
        /// </summary>
        public List<double> matrix { get; set; }
        /// <summary>
        /// The indices of this node's children.
        /// </summary>
        public List<int> children { get; set; }
        /// <summary>
        /// The extras describing this node.
        /// </summary>
        public glTFExtras extras { get; set; }
    }

    public class HashedType
    {
        public string hashcode { get; set; }
    }

    public class MeshContainer : HashedType
    {
        //public string hashcode { get; set; }
        public glTFMesh contents { get; set; }
    }

    /// <summary>
    /// The array of primitives defining the mesh of an object.
    /// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#meshes
    /// </summary>
    [Serializable]
    public class glTFMesh
    {
        public List<glTFMeshPrimitive> primitives { get; set; }
    }

    /// <summary>
    /// Properties defining where the GPU should look to find the mesh and material data.
    /// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#meshes
    /// </summary>
    [Serializable]
    public class glTFMeshPrimitive
    {
        public glTFAttribute attributes { get; set; } = new glTFAttribute();
        public int indices { get; set; }
        public int? material { get; set; } = null;
        public int mode { get; set; } = 4; // 4 is triangles
    }

    /// <summary>
    /// The glTF PBR Material format.
    /// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#materials
    /// </summary>
    public class glTFMaterial
    {
        public string name { get; set; }
        public glTFPBR pbrMetallicRoughness { get; set; }
    }
    public class glTFPBR
    {
        public List<float> baseColorFactor { get; set; }
        public float metallicFactor { get; set; }
        public float roughnessFactor { get; set; }
    }

    /// <summary>
    /// The list of accessors available to the renderer for a particular mesh.
    /// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#meshes
    /// </summary>
    [Serializable]
    public class glTFAttribute
    {
        /// <summary>
        /// The index of the accessor for position data.
        /// </summary>
        public int POSITION { get; set; }
        //public int NORMAL { get; set; }
    }

    /// <summary>
    /// A reference to the location and size of binary data.
    /// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#buffers-and-buffer-views
    /// </summary>
    public class glTFBuffer
    {
        /// <summary>
        /// The uri of the buffer.
        /// </summary>
        public string uri { get; set; }
        /// <summary>
        /// The total byte length of the buffer.
        /// </summary>
        public int byteLength { get; set; }
    }

    /// <summary>
    /// A reference to a subsection of a buffer containing either vector or scalar data.
    /// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#buffers-and-buffer-views
    /// </summary>
    public class glTFBufferView
    {
        /// <summary>
        /// The index of the buffer.
        /// </summary>
        public int buffer { get; set; }
        /// <summary>
        /// The offset into the buffer in bytes.
        /// </summary>
        public int byteOffset { get; set; }
        /// <summary>
        /// The length of the bufferView in bytes.
        /// </summary>
        public int byteLength { get; set; }
        /// <summary>
        /// The target that the GPU buffer should be bound to.
        /// </summary>
        public Targets target { get; set; }
        /// <summary>
        /// A user defined name for this view.
        /// </summary>
        public string name { get; set; }
    }

    /// <summary>
    /// A reference to a subsection of a BufferView containing a particular data type.
    /// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#accessors
    /// </summary>
    public class glTFAccessor
    {
        /// <summary>
        /// The index of the bufferView.
        /// </summary>
        public int bufferView { get; set; }
        /// <summary>
        /// The offset relative to the start of the bufferView in bytes.
        /// </summary>
        public int byteOffset { get; set; }
        /// <summary>
        /// the datatype of the components in the attribute
        /// </summary>
        public ComponentType componentType { get; set; }
        /// <summary>
        /// The number of attributes referenced by this accessor.
        /// </summary>
        public int count { get; set; }
        /// <summary>
        /// Specifies if the attribute is a scalar, vector, or matrix
        /// </summary>
        public string type { get; set; }
        /// <summary>
        /// Maximum value of each component in this attribute.
        /// </summary>
        public List<float> max { get; set; }
        /// <summary>
        /// Minimum value of each component in this attribute.
        /// </summary>
        public List<float> min { get; set; }
        /// <summary>
        /// A user defined name for this accessor.
        /// </summary>
        public string name { get; set; }
    }

    public class glTFExtras
    {
        /// <summary>
        /// The Revit created UniqueId for this object
        /// </summary>
        public string UniqueId { get; set; }
        public GridParameters GridParameters { get; set; }
        public Dictionary<string, string> Properties { get; set; }
    }

    public class GridParameters
    {
        public List<double> origin { get; set; }
        public List<double> direction { get; set; }
        public double length { get; set; }
    }

    //public class glTFFunctions
    //{
    //    public static glTFBinaryData getMeshData(glTFNode node, glTF gltf)
    //    {
    //        if(node.mesh.HasValue)
    //        {
    //            glTFMesh mesh = gltf.meshes[node.mesh.Value];
    //            mesh.
    //        }
    //    }
    //}
}
