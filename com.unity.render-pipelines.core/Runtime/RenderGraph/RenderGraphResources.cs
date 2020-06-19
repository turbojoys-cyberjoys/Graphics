using System.Diagnostics;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.RenderGraphModule
{
    // RendererList is a different case so not represented here.
    internal enum RenderGraphResourceType
    {
        Texture = 0,
        ComputeBuffer,
        Count
    }

    // Can't have a default constructor with handle = -1 hence the ugly IsValid implementation (where m_IsValid will be false by default).
    internal struct ResourceHandle
    {
        bool m_IsValid;

        public int index { get; private set; }
        public RenderGraphResourceType type { get; private set; }
        public int iType { get { return (int)type; } }

        internal ResourceHandle(int value, RenderGraphResourceType type)
        {
            index = value;
            this.type = type;
            m_IsValid = true;
        }

        public static implicit operator int(ResourceHandle handle) => handle.index;
        public bool IsValid() => m_IsValid;
    }

    // BEHOLD C# COPY PASTA
    // Struct can't be inherited and can't have default member values or constructor
    // Hence the copy paste and the ugly IsValid implementation.

    /// <summary>
    /// Texture resource handle.
    /// </summary>
    [DebuggerDisplay("Texture ({handle})")]
    public struct TextureHandle
    {
        internal ResourceHandle handle;

        internal TextureHandle(int handle) { this.handle = new ResourceHandle(handle, RenderGraphResourceType.Texture); }

        /// <summary>
        /// Return true if the handle is valid.
        /// </summary>
        /// <returns>True if the handle is valid.</returns>
        public bool IsValid() => handle.IsValid();
    }

    /// <summary>
    /// Compute Buffer resource handle.
    /// </summary>
    [DebuggerDisplay("ComputeBuffer ({handle})")]
    public struct ComputeBufferHandle
    {
        internal ResourceHandle handle;

        internal ComputeBufferHandle(int handle) { this.handle = new ResourceHandle(handle, RenderGraphResourceType.ComputeBuffer); }

        /// <summary>
        /// Return true if the handle is valid.
        /// </summary>
        /// <returns>True if the handle is valid.</returns>
        public bool IsValid() => handle.IsValid();
    }

    /// <summary>
    /// Renderer List resource handle.
    /// </summary>
    [DebuggerDisplay("RendererList ({handle})")]
    public struct RendererListHandle
    {
        bool m_IsValid;
        internal int handle { get; private set; }
        internal RendererListHandle(int handle) { this.handle = handle; m_IsValid = true; }
        /// <summary>
        /// Conversion to int.
        /// </summary>
        /// <param name="handle">Renderer List handle to convert.</param>
        /// <returns>The integer representation of the handle.</returns>
        public static implicit operator int(RendererListHandle handle) { return handle.handle; }

        /// <summary>
        /// Return true if the handle is valid.
        /// </summary>
        /// <returns>True if the handle is valid.</returns>
        public bool IsValid() => m_IsValid;
    }

    /// <summary>
    /// The mode that determines the size of a Texture.
    /// </summary>
    public enum TextureSizeMode
    {
        ///<summary>Explicit size.</summary>
        Explicit,
        ///<summary>Size automatically scaled by a Vector.</summary>
        Scale,
        ///<summary>Size automatically scaled by a Functor.</summary>
        Functor
    }

    /// <summary>
    /// Subset of the texture desc containing information for fast memory allocation (when platform supports it)
    /// </summary>
    public struct FastMemoryDesc
    {
        ///<summary>Whether the texture will be in fast memory.</summary>
        public bool inFastMemory;
        ///<summary>Flag to determine what parts of the render target is spilled if not fully resident in fast memory.</summary>
        public FastMemoryFlags flags;
        ///<summary>How much of the render target is to be switched into fast memory (between 0 and 1).</summary>
        public float residencyFraction;
    }

    /// <summary>
    /// Descriptor used to create texture resources
    /// </summary>
    public struct TextureDesc
    {
        ///<summary>Texture sizing mode.</summary>
        public TextureSizeMode sizeMode;
        ///<summary>Texture width.</summary>
        public int width;
        ///<summary>Texture height.</summary>
        public int height;
        ///<summary>Number of texture slices..</summary>
        public int slices;
        ///<summary>Texture scale.</summary>
        public Vector2 scale;
        ///<summary>Texture scale function.</summary>
        public ScaleFunc func;
        ///<summary>Depth buffer bit depth.</summary>
        public DepthBits depthBufferBits;
        ///<summary>Color format.</summary>
        public GraphicsFormat colorFormat;
        ///<summary>Filtering mode.</summary>
        public FilterMode filterMode;
        ///<summary>Addressing mode.</summary>
        public TextureWrapMode wrapMode;
        ///<summary>Texture dimension.</summary>
        public TextureDimension dimension;
        ///<summary>Enable random UAV read/write on the texture.</summary>
        public bool enableRandomWrite;
        ///<summary>Texture needs mip maps.</summary>
        public bool useMipMap;
        ///<summary>Automatically generate mip maps.</summary>
        public bool autoGenerateMips;
        ///<summary>Texture is a shadow map.</summary>
        public bool isShadowMap;
        ///<summary>Anisotropic filtering level.</summary>
        public int anisoLevel;
        ///<summary>Mip map bias.</summary>
        public float mipMapBias;
        ///<summary>Textre is multisampled. Only supported for Scale and Functor size mode.</summary>
        public bool enableMSAA;
        ///<summary>Number of MSAA samples. Only supported for Explicit size mode.</summary>
        public MSAASamples msaaSamples;
        ///<summary>Bind texture multi sampled.</summary>
        public bool bindTextureMS;
        ///<summary>Texture uses dynamic scaling.</summary>
        public bool useDynamicScale;
        ///<summary>Memory less flag.</summary>
        public RenderTextureMemoryless memoryless;
        ///<summary>Texture name.</summary>
        public string name;
        ///<summary>Descriptor to determine how the texture will be in fast memory on platform that supports it.</summary>
        public FastMemoryDesc fastMemoryDesc;

        // Initial state. Those should not be used in the hash
        ///<summary>Texture needs to be cleared on first use.</summary>
        public bool clearBuffer;
        ///<summary>Clear color.</summary>
        public Color clearColor;

        void InitDefaultValues(bool dynamicResolution, bool xrReady)
        {
            useDynamicScale = dynamicResolution;
            // XR Ready
            if (xrReady)
            {
                slices = TextureXR.slices;
                dimension = TextureXR.dimension;
            }
            else
            {
                slices = 1;
                dimension = TextureDimension.Tex2D;
            }
        }

        /// <summary>
        /// TextureDesc constructor for a texture using explicit size
        /// </summary>
        /// <param name="width">Texture width</param>
        /// <param name="height">Texture height</param>
        /// <param name="dynamicResolution">Use dynamic resolution</param>
        /// <param name="xrReady">Set this to true if the Texture is a render texture in an XR setting.</param>
        public TextureDesc(int width, int height, bool dynamicResolution = false, bool xrReady = false)
            : this()
        {
            // Size related init
            sizeMode = TextureSizeMode.Explicit;
            this.width = width;
            this.height = height;
            // Important default values not handled by zero construction in this()
            msaaSamples = MSAASamples.None;
            InitDefaultValues(dynamicResolution, xrReady);
        }

        /// <summary>
        /// TextureDesc constructor for a texture using a fixed scaling
        /// </summary>
        /// <param name="scale">RTHandle scale used for this texture</param>
        /// <param name="dynamicResolution">Use dynamic resolution</param>
        /// <param name="xrReady">Set this to true if the Texture is a render texture in an XR setting.</param>
        public TextureDesc(Vector2 scale, bool dynamicResolution = false, bool xrReady = false)
            : this()
        {
            // Size related init
            sizeMode = TextureSizeMode.Scale;
            this.scale = scale;
            // Important default values not handled by zero construction in this()
            msaaSamples = MSAASamples.None;
            dimension = TextureDimension.Tex2D;
            InitDefaultValues(dynamicResolution, xrReady);
        }

        /// <summary>
        /// TextureDesc constructor for a texture using a functor for scaling
        /// </summary>
        /// <param name="func">Function used to determnine the texture size</param>
        /// <param name="dynamicResolution">Use dynamic resolution</param>
        /// <param name="xrReady">Set this to true if the Texture is a render texture in an XR setting.</param>
        public TextureDesc(ScaleFunc func, bool dynamicResolution = false, bool xrReady = false)
            : this()
        {
            // Size related init
            sizeMode = TextureSizeMode.Functor;
            this.func = func;
            // Important default values not handled by zero construction in this()
            msaaSamples = MSAASamples.None;
            dimension = TextureDimension.Tex2D;
            InitDefaultValues(dynamicResolution, xrReady);
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="input"></param>
        public TextureDesc(TextureDesc input)
        {
            this = input;
        }

        /// <summary>
        /// Hash function
        /// </summary>
        /// <returns>The texture descriptor hash.</returns>
        public override int GetHashCode()
        {
            int hashCode = 17;

            unchecked
            {
                switch (sizeMode)
                {
                    case TextureSizeMode.Explicit:
                        hashCode = hashCode * 23 + width;
                        hashCode = hashCode * 23 + height;
                        hashCode = hashCode * 23 + (int)msaaSamples;
                        break;
                    case TextureSizeMode.Functor:
                        if (func != null)
                            hashCode = hashCode * 23 + func.GetHashCode();
                        hashCode = hashCode * 23 + (enableMSAA ? 1 : 0);
                        break;
                    case TextureSizeMode.Scale:
                        hashCode = hashCode * 23 + scale.x.GetHashCode();
                        hashCode = hashCode * 23 + scale.y.GetHashCode();
                        hashCode = hashCode * 23 + (enableMSAA ? 1 : 0);
                        break;
                }

                hashCode = hashCode * 23 + mipMapBias.GetHashCode();
                hashCode = hashCode * 23 + slices;
                hashCode = hashCode * 23 + (int)depthBufferBits;
                hashCode = hashCode * 23 + (int)colorFormat;
                hashCode = hashCode * 23 + (int)filterMode;
                hashCode = hashCode * 23 + (int)wrapMode;
                hashCode = hashCode * 23 + (int)dimension;
                hashCode = hashCode * 23 + (int)memoryless;
                hashCode = hashCode * 23 + anisoLevel;
                hashCode = hashCode * 23 + (enableRandomWrite ? 1 : 0);
                hashCode = hashCode * 23 + (useMipMap ? 1 : 0);
                hashCode = hashCode * 23 + (autoGenerateMips ? 1 : 0);
                hashCode = hashCode * 23 + (isShadowMap ? 1 : 0);
                hashCode = hashCode * 23 + (bindTextureMS ? 1 : 0);
                hashCode = hashCode * 23 + (useDynamicScale ? 1 : 0);
            }

            return hashCode;
        }
    }

    /// <summary>
    /// Descriptor used to create compute buffer resources
    /// </summary>
    public struct ComputeBufferDesc
    {
        ///<summary>Number of elements in the buffer..</summary>
        public int count;
        ///<summary>Size of one element in the buffer. Has to match size of buffer type in the shader.</summary>
        public int stride;
        ///<summary>Type of the buffer, default is ComputeBufferType.Default (structured buffer).</summary>
        public ComputeBufferType type;
        /// <summary>Compute Buffer name.</summary>
        public string name;

        /// <summary>
        /// ComputeBufferDesc constructor.
        /// </summary>
        /// <param name="count">Number of elements in the buffer.</param>
        /// <param name="stride">Size of one element in the buffer.</param>
        public ComputeBufferDesc(int count, int stride)
            : this()
        {
            this.count = count;
            this.stride = stride;
            type = ComputeBufferType.Default;
        }

        /// <summary>
        /// ComputeBufferDesc constructor.
        /// </summary>
        /// <param name="count">Number of elements in the buffer.</param>
        /// <param name="stride">Size of one element in the buffer.</param>
        /// <param name="type">Type of the buffer.</param>
        public ComputeBufferDesc(int count, int stride, ComputeBufferType type)
            : this()
        {
            this.count = count;
            this.stride = stride;
            this.type = type;
        }

        /// <summary>
        /// Hash function
        /// </summary>
        /// <returns>The texture descriptor hash.</returns>
        public override int GetHashCode()
        {
            int hashCode = 17;

            hashCode = hashCode * 23 + count;
            hashCode = hashCode * 23 + stride;
            hashCode = hashCode * 23 + (int)type;

            return hashCode;
        }
    }

}
