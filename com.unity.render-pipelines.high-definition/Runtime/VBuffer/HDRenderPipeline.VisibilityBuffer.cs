using System.Collections.Generic;
using UnityEngine.VFX;
using System;
using System.Diagnostics;
using System.Linq;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;

namespace UnityEngine.Rendering.HighDefinition
{
    public partial class HDRenderPipeline
    {
        internal struct VBufferOutput
        {
            public TextureHandle vBuffer0;
            public TextureHandle depthBuffer;
        }

        class VBufferPassData
        {
            public int clusterCount;
            public Material renderVisibilityMaterial;
            public TextureHandle tempColorBuffer;
            public TextureHandle vbuffer0;
            public TextureHandle materialDepthBuffer;
            public TextureHandle depthBuffer;
            public FrameSettings frameSettings;
        }

        VBufferOutput RenderVBuffer(RenderGraph renderGraph, CullingResults cullingResults, HDCamera hdCamera, TextureHandle tempColorBuffer)
        {
            VBufferOutput vBufferOutput = new VBufferOutput();
            if (InstanceVDataB == null || CompactedVB == null || CompactedIB == null) return vBufferOutput;

            // These flags are still required in SRP or the engine won't compute previous model matrices...
            // If the flag hasn't been set yet on this camera, motion vectors will skip a frame.
            hdCamera.camera.depthTextureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;

            using (var builder = renderGraph.AddRenderPass<VBufferPassData>("VBuffer Prepass", out var passData, ProfilingSampler.Get(HDProfileId.VBufferPrepass)))
            {
                builder.AllowRendererListCulling(false);

                passData.clusterCount = InstanceVDataB.count;
                passData.renderVisibilityMaterial = m_VisibilityBufferMaterial;
                passData.tempColorBuffer = builder.WriteTexture(tempColorBuffer);
                passData.vbuffer0 = builder.WriteTexture(renderGraph.CreateTexture(new TextureDesc(Vector2.one, true, true)
                    { colorFormat = GraphicsFormat.R32_UInt, clearBuffer = true, enableRandomWrite = true, name = "VBuffer 0" }));
                passData.materialDepthBuffer = builder.WriteTexture(renderGraph.CreateTexture(new TextureDesc(Vector2.one, true, true)
                    { colorFormat = GraphicsFormat.R16_UInt, clearBuffer = true, enableRandomWrite = true, name = "Material Buffer" }));

                passData.depthBuffer = CreateDepthBuffer(renderGraph, true, hdCamera.msaaSamples);

                builder.UseDepthBuffer(passData.depthBuffer, DepthAccess.ReadWrite);
                builder.UseColorBuffer(passData.vbuffer0, 0);
                builder.UseColorBuffer(passData.materialDepthBuffer, 1);

                passData.frameSettings = hdCamera.frameSettings;

                builder.SetRenderFunc(
                    (VBufferPassData data, RenderGraphContext context) =>
                    {
                        context.cmd.SetGlobalBuffer("_CompactedVertexBuffer", CompactedVB);
                        context.cmd.SetGlobalBuffer("_CompactedIndexBuffer", CompactedIB);
                        context.cmd.SetGlobalBuffer("_InstanceVDataBuffer", InstanceVDataB);
                        context.cmd.DrawProcedural(Matrix4x4.identity, data.renderVisibilityMaterial, 0, MeshTopology.Triangles, VisibilityBufferConstants.s_ClusterSizeInIndices, data.clusterCount);
                    });

                vBufferOutput.vBuffer0 = passData.vbuffer0;
                vBufferOutput.depthBuffer = passData.depthBuffer;

                PushFullScreenDebugTexture(renderGraph, vBufferOutput.vBuffer0, FullScreenDebugMode.VBufferTriangleId, GraphicsFormat.R32_UInt);
                PushFullScreenDebugTexture(renderGraph, vBufferOutput.vBuffer0, FullScreenDebugMode.VBufferGeometryId, GraphicsFormat.R32_UInt);
            }
            return vBufferOutput;
        }

        class VBufferLightingPassData
        {
            public TextureHandle colorBuffer;
            public TextureHandle vbuffer0;
            public TextureHandle materialDepthBuffer;
            public ComputeBufferHandle vertexBuffer;
            public ComputeBufferHandle indexBuffer;
            public ComputeBufferHandle instancedDataBuffer;
            public ComputeBufferHandle lightListBuffer;
            public ComputeBufferHandle tileFeatureFlagsBuffer;
            public ComputeBufferHandle tileListBuffer;
        }

        TextureHandle RenderVBufferLighting(RenderGraph renderGraph, CullingResults cullingResults, HDCamera hdCamera, VBufferOutput vBufferOutput, TextureHandle materialDepthBuffer, TextureHandle colorBuffer,
            in BuildGPULightListOutput lightLists)
        {
            if (InstanceVDataB == null || CompactedVB == null || CompactedIB == null) return colorBuffer;

            using (var builder = renderGraph.AddRenderPass<VBufferLightingPassData>("VBuffer Lighting", out var passData, ProfilingSampler.Get(HDProfileId.VBufferLighting)))
            {
                builder.AllowRendererListCulling(false);

                passData.colorBuffer = builder.UseColorBuffer(colorBuffer, 0);
                passData.vbuffer0 = builder.ReadTexture(vBufferOutput.vBuffer0);
                passData.materialDepthBuffer = builder.UseDepthBuffer(materialDepthBuffer, DepthAccess.ReadWrite);
                passData.vertexBuffer = renderGraph.ImportComputeBuffer(CompactedVB);
                passData.indexBuffer = renderGraph.ImportComputeBuffer(CompactedIB);
                passData.instancedDataBuffer = renderGraph.ImportComputeBuffer(InstanceVDataB);
                passData.lightListBuffer = builder.ReadComputeBuffer(lightLists.lightList);
                passData.tileFeatureFlagsBuffer = builder.ReadComputeBuffer(lightLists.tileFeatureFlags);
                passData.tileListBuffer = builder.ReadComputeBuffer(lightLists.tileList);

                builder.SetRenderFunc(
                    (VBufferLightingPassData data, RenderGraphContext context) =>
                    {
                        context.cmd.SetGlobalBuffer("_CompactedVertexBuffer", data.vertexBuffer);
                        context.cmd.SetGlobalBuffer("_CompactedIndexBuffer", data.indexBuffer);
                        context.cmd.SetGlobalBuffer("_InstanceVDataBuffer", data.instancedDataBuffer);
                        context.cmd.SetGlobalTexture("_VBuffer0", data.vbuffer0);

                        context.cmd.SetGlobalBuffer(HDShaderIDs.g_vLightListGlobal, data.lightListBuffer);


                        var materialList = materials.Keys.ToArray();
                        for (int matIdx = 0; matIdx < materialList.Length; ++matIdx)
                        {
                            var material = materialList[matIdx];

                            var passIdx = -1;
                            for (int i = 0; i < material.passCount; ++i)
                            {
                                if (material.GetPassName(i).IndexOf("VBufferLighting") >= 0)
                                {
                                    passIdx = i;
                                    break;
                                }
                            }

                            if (passIdx == -1) continue;

                            context.cmd.SetGlobalInt("_CurrMaterialID", materials[material]);
                            HDUtils.DrawFullScreen(context.cmd, material, data.colorBuffer, data.materialDepthBuffer, null, shaderPassId: passIdx);
                        }
                    });

                PushFullScreenDebugTexture(renderGraph, colorBuffer, FullScreenDebugMode.VBufferLightingDebug);
            }
            return colorBuffer;
        }

        class VBufferMaterialDepthPassData
        {
            public TextureHandle outputDepthBuffer;
            public TextureHandle dummyColorOutput;
            public Material createMaterialDepthMaterial;
        }

        TextureHandle RenderMaterialDepth(RenderGraph renderGraph, HDCamera hdCamera, TextureHandle colorBuffer)
        {
            var outputDepth = CreateDepthBuffer(renderGraph, true, hdCamera.msaaSamples);
            using (var builder = renderGraph.AddRenderPass<VBufferMaterialDepthPassData>("Create Vis Buffer Material Depth", out var passData, ProfilingSampler.Get(HDProfileId.VBufferMaterialDepth)))
            {
                passData.outputDepthBuffer = outputDepth;
                passData.createMaterialDepthMaterial = m_CreateMaterialDepthMaterial;
                passData.dummyColorOutput = builder.WriteTexture(colorBuffer);
                builder.UseDepthBuffer(passData.outputDepthBuffer, DepthAccess.ReadWrite);

                builder.SetRenderFunc(
                    (VBufferMaterialDepthPassData data, RenderGraphContext context) =>
                    {
                        // Doesn't matter what's bound as color buffer
                        HDUtils.DrawFullScreen(context.cmd, passData.createMaterialDepthMaterial, passData.dummyColorOutput, passData.outputDepthBuffer, null, 0);
                    });

                PushFullScreenDebugTexture(renderGraph, outputDepth, FullScreenDebugMode.VBufferMaterialId, GraphicsFormat.R32_SFloat);
            }

            return outputDepth;
        }

        class VBufferTileClassficationData
        {
            public ComputeShader createTileClassification;
            public TextureHandle outputTile;
            public ComputeBufferHandle tileFeatureFlagsBuffer;
        }

        TextureHandle VBufferTileClassification(RenderGraph renderGraph, HDCamera hdCamera, ComputeBufferHandle tileFeatureFlags, TextureHandle colorBuffer)
        {
            var tileClassification = renderGraph.CreateTexture(new TextureDesc(Vector2.one * (1.0f / 64.0f), true, true)
                { colorFormat = GraphicsFormat.R32G32_UInt, clearBuffer = true, enableRandomWrite = true, name = "Tile classification" });
            using (var builder = renderGraph.AddRenderPass<VBufferTileClassficationData>("Create VBuffer Tiles", out var passData, ProfilingSampler.Get(HDProfileId.VBufferMaterialTileClassification)))
            {
                passData.outputTile = builder.WriteTexture(tileClassification);

                passData.createTileClassification = defaultResources.shaders.classificationTilesCS;
                passData.tileFeatureFlagsBuffer = builder.ReadComputeBuffer(tileFeatureFlags);

                builder.AllowPassCulling(false);

                builder.SetRenderFunc(
                    (VBufferTileClassficationData data, RenderGraphContext context) =>
                    {
                        var cs = data.createTileClassification;
                        var kernel = cs.FindKernel("CreateVisibilityBuffClassification");

                        context.cmd.SetComputeBufferParam(cs, kernel, HDShaderIDs.g_TileFeatureFlags, data.tileFeatureFlagsBuffer);
                        context.cmd.SetComputeTextureParam(cs, kernel, "_ClassificationTile", data.outputTile);

                        int tileClassSizeX = Mathf.RoundToInt(hdCamera.actualWidth * (1.0f / 64.0f));
                        int tileClassSizeY = Mathf.RoundToInt(hdCamera.actualHeight * (1.0f / 64.0f));

                        int dispatchX = HDUtils.DivRoundUp(tileClassSizeX, 8);
                        int dispatchY = HDUtils.DivRoundUp(tileClassSizeY, 8);

                        context.cmd.SetComputeVectorParam(cs, "_TileBufferSize", new Vector4(tileClassSizeX, tileClassSizeY, 0, 0));

                        context.cmd.DispatchCompute(cs, kernel, dispatchX, dispatchY, 1);

                        //// Doesn't matter what's bound as color buffer
                        //HDUtils.DrawFullScreen(context.cmd, passData.createMaterialDepthMaterial, passData.dummyColorOutput, passData.outputDepthBuffer, null, 0);
                    });
            }

            return tileClassification;
        }
    }
}
