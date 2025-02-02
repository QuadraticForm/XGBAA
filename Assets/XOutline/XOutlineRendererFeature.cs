using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class XOutlineRendererFeature : ScriptableRendererFeature
{
	#region Shared Fields

	[Header("Common")]

	public LayerMask layerMask = 0;

	public List<string> shaderTagNameList = new List<string>
	{
		"UniversalForward",
		"UniversalGBuffer", // this is to ensure shaders like UnlitShaderGraph are included
							// (which doesn't have a light mode in UniversalForward, but do have a UniversalGBuffer pass)
	};

	private List<ShaderTagId> shaderTagIdList;

	public enum GBufferPrecision
	{
		Float,
		Half,
	}

	[Space]
	public GBufferPrecision gbufferPrecision = GBufferPrecision.Half;

	// xy: normal in spherical coordinates, zw: delta screen space position between offseted and original
	private TextureHandle gbuffer1 = TextureHandle.nullHandle;

	// DEPRECATED since resolve shader v6, only kept for testing purpose
	// Outline Color and Alpha, 
	// separately stored without blending with camera Color,
	// for coverage bluring in resolve pass.
	private TextureHandle gbuffer2 = TextureHandle.nullHandle;

	XOutlinePreparePass preparePass;

	#endregion

	#region Front Normal Pass

	[Header("Normal Pass")]
	public Material frontNormalMaterial;

	XOutlineFrontNormalPass frontNormalPass;

	#endregion

	#region Outline Passes Fields

	[Header("Outline Pass")]
	public Material outlineMaterial;

	XOutlineOutlinePass outlineGBufferPass;

	#endregion

	#region Resolve Pass Fields

	XOutlinePostProcessPass resolvePass;

	[Header("Resolve Pass")]

	public bool resolveEnabled = true;

	[Range(0, 1), Tooltip("If alpha == 0, won't execute this pass")]
	public float resolveAlpha = 1.0f;

	public Material resolveMaterial;

	public enum ResolveInjectionPoint
	{
		BeforeRenderingPostProcessing = RenderPassEvent.BeforeRenderingPostProcessing,
		AfterRenderingPostProcessing = RenderPassEvent.AfterRenderingPostProcessing,
	}

	public ResolveInjectionPoint resolveInjectionPoint = ResolveInjectionPoint.BeforeRenderingPostProcessing;

	#endregion

	#region Debug Pass Fields

	[Header("Debug Pass")]

	public bool debugEnabled = false;

	[Range(0, 1), Tooltip("If alpha == 0, won't execute this pass")]
	public float debugAlpha = 1.0f;

	public Material debugMaterial;

	XOutlinePostProcessPass debugPass;

	#endregion

	public override void Create()
	{
		shaderTagIdList = new List<ShaderTagId>();
		foreach (var passName in shaderTagNameList)
		{
			shaderTagIdList.Add(new ShaderTagId(passName));
		}

		preparePass = new XOutlinePreparePass(this);
		preparePass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;

		outlineGBufferPass = new XOutlineOutlinePass(this, "XOutline Outline Pass", outlineMaterial);
		frontNormalPass = new XOutlineFrontNormalPass(this, "XOutline Front Normal Pass", frontNormalMaterial);
		resolvePass = new XOutlinePostProcessPass(this, resolveMaterial, resolveAlpha);
		debugPass = new XOutlinePostProcessPass(this, debugMaterial, debugAlpha);
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		// set injection points

		outlineGBufferPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
		frontNormalPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
		resolvePass.renderPassEvent = (RenderPassEvent)resolveInjectionPoint;
		debugPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

		// enqueue passes

		renderer.EnqueuePass(preparePass);
		renderer.EnqueuePass(outlineGBufferPass);
		renderer.EnqueuePass(frontNormalPass);

		if (resolveEnabled && resolveAlpha > 0.0f)
			renderer.EnqueuePass(resolvePass);

		if (debugEnabled && debugAlpha > 0.0f)
			renderer.EnqueuePass(debugPass);
	}

	class XOutlinePreparePass : ScriptableRenderPass
	{ 
		protected XOutlineRendererFeature rendererFeature;

		public XOutlinePreparePass(XOutlineRendererFeature rendererFeature)
		{
			this.rendererFeature = rendererFeature;
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameContext)
		{
			using (var builder = renderGraph.AddRasterRenderPass<object>("XOutline Prepare GBuffer", out var emptyPassData))
			{
				var cameraData = frameContext.Get<UniversalCameraData>();

				var textureProperties = cameraData.cameraTargetDescriptor;
				textureProperties.depthBufferBits = 0;

				// create gbuffer 1

				if (rendererFeature.gbufferPrecision == GBufferPrecision.Half)
					textureProperties.colorFormat = RenderTextureFormat.ARGBHalf;
				else
					textureProperties.colorFormat = RenderTextureFormat.ARGBFloat;

				rendererFeature.gbuffer1 = UniversalRenderer.CreateRenderGraphTexture(renderGraph, textureProperties, "XOutline GBuffer 1", false);

				// create gbuffer 2
				// DEPRECATED since resolve shader v6, only kept for testing purpose

				textureProperties.colorFormat = RenderTextureFormat.ARGB32;

				rendererFeature.gbuffer2 = UniversalRenderer.CreateRenderGraphTexture(renderGraph, textureProperties, "XOutline GBuffer 2", false);

				// clear them

				builder.SetRenderAttachment(rendererFeature.gbuffer1, 0);
				builder.SetRenderAttachment(rendererFeature.gbuffer2, 1);

				builder.SetRenderFunc((object passData, RasterGraphContext context) =>
				{
					context.cmd.ClearRenderTarget(false, true, new Color(0, 0, 0, 0));
				});
			}
		}
	}


	class XOutlineDrawObjectsPass : ScriptableRenderPass
	{
		protected string name;
		protected XOutlineRendererFeature rendererFeature;
		protected Material overrideMaterial;

		protected UniversalRenderingData renderingData;
		protected UniversalResourceData resourceData;
		protected UniversalCameraData cameraData;
		protected UniversalLightData lightData;

		protected RendererListHandle rendererListHandle;

		public static class ShaderPropertyId
		{
			public static readonly int IsGBufferPass = Shader.PropertyToID("_IsGBufferPass");
		}

		public XOutlineDrawObjectsPass(XOutlineRendererFeature rendererFeature, string name, Material overrideMaterial = null)
		{
			this.name = name;
			this.rendererFeature = rendererFeature;
			this.overrideMaterial = overrideMaterial;
		}

		protected virtual void CreateRendererList(RenderGraph renderGraph)
		{
			var sortFlags = cameraData.defaultOpaqueSortFlags;
			var drawSettings = RenderingUtils.CreateDrawingSettings(rendererFeature.shaderTagIdList, renderingData, cameraData, lightData, sortFlags);

			if (overrideMaterial != null)
			{
				drawSettings.overrideMaterial = overrideMaterial;
			}

			var filterSettings = new FilteringSettings(RenderQueueRange.all, rendererFeature.layerMask);

			var rendererListParameters = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);
			rendererListHandle = renderGraph.CreateRendererList(rendererListParameters);
		}

		protected virtual void SetupRenderTargets(IRasterRenderGraphBuilder builder, RenderGraph renderGraph)
		{
		}

		protected virtual void SetRenderFunc(IRasterRenderGraphBuilder builder)
		{
			
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameContext)
		{
			using (var builder = renderGraph.AddRasterRenderPass<object>(name, out var emptyPassData))
			{
				// get all sorts of data from the frame context

				renderingData = frameContext.Get<UniversalRenderingData>();
				resourceData = frameContext.Get<UniversalResourceData>();
				cameraData = frameContext.Get<UniversalCameraData>();
				lightData = frameContext.Get<UniversalLightData>();

				// create renderer list

				CreateRendererList(renderGraph);

				// create render target

				SetupRenderTargets(builder, renderGraph);

				// actual build render graph

				SetRenderFunc(builder);
			}
		}
	}

	class XOutlineOutlinePass : XOutlineDrawObjectsPass
	{
		public XOutlineOutlinePass(XOutlineRendererFeature rendererFeature, string name, Material overrideMaterial = null)
			: base(rendererFeature, name, overrideMaterial)
		{
		}

		protected override void SetupRenderTargets(IRasterRenderGraphBuilder builder, RenderGraph renderGraph)
		{
			// Render To Camera Color
			builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
			builder.SetRenderAttachment(rendererFeature.gbuffer1, 1);
			builder.SetRenderAttachment(rendererFeature.gbuffer2, 2); // DEPRECATED since resolve shader v6, only kept for testing purpose

			// Render To GBuffer Only
			// builder.SetRenderAttachment(rendererFeature.gbuffer1, 0);
			// builder.SetRenderAttachment(rendererFeature.gbuffer2, 1); // DEPRECATED since resolve shader v6, only kept for testing purpose

			builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);
		}

		protected override void SetRenderFunc(IRasterRenderGraphBuilder builder)
		{
			builder.UseRendererList(rendererListHandle);

			builder.SetRenderFunc((object passData, RasterGraphContext context) =>
			{
				context.cmd.DrawRendererList(rendererListHandle);
			});
		}
	}

	// Renders the view space normals of the front faces of objects
	// this pass can be skipped if the pipeline already has a normal pass
	class XOutlineFrontNormalPass : XOutlineDrawObjectsPass
	{
		public XOutlineFrontNormalPass(XOutlineRendererFeature rendererFeature, string name, Material overrideMaterial = null)
			: base(rendererFeature, name, overrideMaterial)
		{
		}

		protected override void SetupRenderTargets(IRasterRenderGraphBuilder builder, RenderGraph renderGraph)
		{
			builder.SetRenderAttachment(rendererFeature.gbuffer1, 0);
			builder.SetRenderAttachment(rendererFeature.gbuffer2, 1); // DEPRECATED since resolve shader v6, only kept for testing purpose
			builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);
		}

		protected override void SetRenderFunc(IRasterRenderGraphBuilder builder)
		{
			builder.UseRendererList(rendererListHandle);

			builder.SetRenderFunc((object passData, RasterGraphContext context) =>
			{
				context.cmd.DrawRendererList(rendererListHandle);
			});
		}
	}

	class XOutlinePostProcessPass : ScriptableRenderPass
	{
		private class CopyCameraColorPassData
		{
			public TextureHandle source;
			public TextureHandle destination;
		}

		private class MainPassData
		{
			public TextureHandle cameraColorCopy;
			public TextureHandle cameraDepth;
			public TextureHandle gbuffer;
			public TextureHandle gbuffer2; // DEPRECATED since resolve shader v6, only kept for testing purpose

			public TextureHandle destination;
		}

		public Material postProcessMaterial;
		public float alpha = 1;
		private XOutlineRendererFeature rendererFeature;
		private MaterialPropertyBlock propertyBlock;

		public XOutlinePostProcessPass(XOutlineRendererFeature rendererFeature, Material postProcessMaterial, float alpha)
		{
			this.rendererFeature = rendererFeature;
			propertyBlock = new MaterialPropertyBlock();
			this.postProcessMaterial = postProcessMaterial;
			this.alpha = alpha;
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameContext)
		{
			if (postProcessMaterial == null || alpha < 0.000001)
				return;

			// get all sorts of data from the frame context

			var resourcesData = frameContext.Get<UniversalResourceData>();
			var cameraData = frameContext.Get<UniversalCameraData>();

			// create a texture to copy current active color texture to

			var targetDesc = renderGraph.GetTextureDesc(resourcesData.cameraColor);
			targetDesc.name = "XOutline Camera Color";
			targetDesc.clearBuffer = false;

			var cameraColorCopy = renderGraph.CreateTexture(targetDesc);

			// build render graph for copying camera color

			using (var builder = renderGraph.AddRasterRenderPass<CopyCameraColorPassData>("XOutline Copy Camera Color", out var passData, profilingSampler))
			{
				passData.source = resourcesData.activeColorTexture;
				passData.destination = cameraColorCopy;

				builder.UseTexture(passData.source);

				builder.SetRenderAttachment(passData.destination, 0);

				builder.SetRenderFunc((CopyCameraColorPassData data, RasterGraphContext context) =>
				{
					Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0.0f, false);
				});
			}

			// build render graph for post process pass

			using (var builder = renderGraph.AddRasterRenderPass<MainPassData>("XOutline Post-Process", out var passData, profilingSampler))
			{
				passData.cameraColorCopy = cameraColorCopy;
				passData.cameraDepth = resourcesData.cameraDepthTexture;
				passData.gbuffer = rendererFeature.gbuffer1;
				passData.gbuffer2 = rendererFeature.gbuffer2; // DEPRECATED since resolve shader v6, only kept for testing purpose
				passData.destination = resourcesData.activeColorTexture;

				builder.UseTexture(passData.cameraColorCopy);
				builder.UseTexture(passData.cameraDepth);
				builder.UseTexture(passData.gbuffer);
				builder.UseTexture(passData.gbuffer2); // DEPRECATED since resolve shader v6, only kept for testing purpose
				builder.SetRenderAttachment(passData.destination, 0);

				builder.SetRenderFunc((MainPassData data, RasterGraphContext context) =>
				{
					propertyBlock.SetTexture("_CameraColorCopy", data.cameraColorCopy);
					// propertyBlock.SetTexture("_CameraDepthCopy", data.cameraDepth); // "_CameraDepthTexture" is in use by unity, so I just use "_CameraDepthCopy" instead
					propertyBlock.SetTexture("_GBuffer", data.gbuffer);
					propertyBlock.SetTexture("_GBuffer2", data.gbuffer2); // DEPRECATED since resolve shader v6, only kept for testing purpose
					propertyBlock.SetFloat("_Alpha", alpha);

					// var material = rendererFeature.debugGBufferAlpha > 0.01f ? rendererFeature.debugMaterial : rendererFeature.resolveMaterial;

					// copied form Unity URP's FullScreenPassRendererFeature.cs
					// it seems the FullScreen Shader Graph determines the vertex position based on vertex index
					// and it made this triangle large enough to cover the whole screen
					context.cmd.DrawProcedural(Matrix4x4.identity, postProcessMaterial, 0, MeshTopology.Triangles, 3, 1, propertyBlock);
				});
			}
		}
	}
}
