using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Conditional = System.Diagnostics.ConditionalAttribute;

//------------------------------------------------------------------------------
[CreateAssetMenu(menuName = "Rendering/Linden Pipeline")]
public class LindenPipelineAsset : RenderPipelineAsset {

    [SerializeField]
    private bool dynamicBatching;
    [SerializeField]
    private bool instancing;

    protected override IRenderPipeline InternalCreatePipeline () {
        return new LindenPipeline(dynamicBatching, instancing);
    }

}

//------------------------------------------------------------------------------
public class LindenPipeline : RenderPipeline {

    //------------------------------------------------------------------------------
    // RENDER SETTINGS
    //------------------------------------------------------------------------------
    Material errorMaterial;
    DrawRendererFlags drawFlags;
    CullResults cullResults;

    //------------------------------------------------------------------------------
    // BUFFERS
    //------------------------------------------------------------------------------
    CommandBuffer cameraBuffer = new CommandBuffer { name = "Camera" };

    //------------------------------------------------------------------------------
    // LIGHTING SETTINGS
    //------------------------------------------------------------------------------
    // 'vectors' as in directions or positions
    // based on whether they're directional or point

    const int maxVisibleLights = 16;
    
    static int lightIndicesOffsetAndCountID = Shader.PropertyToID("unity_LightIndicesOffsetAndCount");
    static int visibleLightColorsId = Shader.PropertyToID("_VisibleLightColors");
    static int visibleLightVectorsId = Shader.PropertyToID("_VisibleLightVectors");
    static int visibleLightAttenuationsId = Shader.PropertyToID("_VisibleLightAttenuations");
    static int visibleLightSpotDirectionsId = Shader.PropertyToID("_VisibleLightSpotDirections");

    Vector4[] visibleLightColors = new Vector4[maxVisibleLights];
    Vector4[] visibleLightVectors = new Vector4[maxVisibleLights];
    Vector4[] visibleLightAttenuations = new Vector4[maxVisibleLights];
    Vector4[] visibleLightSpotDirections = new Vector4[maxVisibleLights];

    //------------------------------------------------------------------------------
    // PUBILC FUNCTIONS
    //------------------------------------------------------------------------------
    public LindenPipeline (bool dynamicBatching, bool instancing) {
        if(dynamicBatching) {
            drawFlags = DrawRendererFlags.EnableDynamicBatching;
        }
        if(instancing) {
            drawFlags |= DrawRendererFlags.EnableInstancing;
        }
    }

    //------------------------------------------------------------------------------
    public override void Render (ScriptableRenderContext renderContext, Camera[] cameras) {
        base.Render(renderContext, cameras);

        foreach(var camera in cameras) {
            Render(renderContext, camera);
        }
    }

    //------------------------------------------------------------------------------
    // PRIVATE FUNCTIONS
    //------------------------------------------------------------------------------
    private void Render (ScriptableRenderContext context, Camera camera) {
        ScriptableCullingParameters cullingParameters;
        if(!CullResults.GetCullingParameters(camera, out cullingParameters)) {
            return;
        }
        CullResults.Cull(ref cullingParameters, context, ref cullResults);

        // pass builtin global vars to GPU
        context.SetupCameraProperties(camera);

        // clear buffer
        CameraClearFlags clearFlags = camera.clearFlags;
        cameraBuffer.ClearRenderTarget(
            (clearFlags & CameraClearFlags.Depth) != 0,
            (clearFlags & CameraClearFlags.Color) != 0,
            camera.backgroundColor
        );

        // prepare light data
        if(cullResults.visibleLights.Count > 0) {
            ConfigureLights();
        } else {
            cameraBuffer.SetGlobalVector (
                lightIndicesOffsetAndCountID, Vector4.zero
            );
        }

        cameraBuffer.BeginSample("Camera");

        // pass custom global shader vars to GPU
        cameraBuffer.SetGlobalVectorArray (
            visibleLightColorsId, visibleLightColors
        );
        cameraBuffer.SetGlobalVectorArray (
            visibleLightVectorsId, visibleLightVectors
        );
        cameraBuffer.SetGlobalVectorArray (
            visibleLightAttenuationsId, visibleLightAttenuations
        );
        cameraBuffer.SetGlobalVectorArray (
            visibleLightSpotDirectionsId, visibleLightSpotDirections
        );

        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();

        // draw opaques
        var drawSettings = new DrawRendererSettings(
            camera, new ShaderPassName("SRPDefaultUnlit")
        ) {
            flags = drawFlags
        };
        if(cullResults.visibleLights.Count > 0) {
            drawSettings.rendererConfiguration = RendererConfiguration.PerObjectLightIndices8;
        }
        drawSettings.sorting.flags = SortFlags.CommonOpaque;

        var filterSettings = new FilterRenderersSettings(true) {
            renderQueueRange = RenderQueueRange.opaque
        };

        context.DrawRenderers (
            cullResults.visibleRenderers, ref drawSettings, filterSettings
        );

        // draw skybox
        context.DrawSkybox(camera);

        // draw transparents
        drawSettings.sorting.flags = SortFlags.CommonTransparent;
        filterSettings.renderQueueRange = RenderQueueRange.transparent;

        context.DrawRenderers (
            cullResults.visibleRenderers, ref drawSettings, filterSettings
        );

        // draw error material
        DrawDefaultPipeline(context, camera);

        cameraBuffer.EndSample("Camera");
        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();

        context.Submit();
    }

    //------------------------------------------------------------------------------
    private void ConfigureLights () {
        for(int i = 0; i < cullResults.visibleLights.Count; i++) {
            // might try to put more lights in the scene than supported in pipeline
            if (i == maxVisibleLights) {
                Debug.LogError("Attempting to use more lights in the scene than the renderer supports. Max supported lights: " + maxVisibleLights);
                break;
            }

            // set light color
            VisibleLight light = cullResults.visibleLights[i];
            visibleLightColors[i] = light.finalColor;

            // set light vector, attentuation, & spotdirection
            //   based on light type
            Vector4 a = Vector4.zero;
            a.w = 1.0f;
            if(light.lightType == LightType.Directional) {
                // 3rd column is z-axis of light, which is the direction it shines in
                Vector4 v = light.localToWorld.GetColumn(2);
                // negate so vector is pointing towards the light
                v.x = -v.x;
                v.y = -v.y;
                v.z = -v.z;
                visibleLightVectors[i] = v;
            }
            else {
                // 3rd column is position
                visibleLightVectors[i] = light.localToWorld.GetColumn(3);
                a.x = 1.0f / Mathf.Max(light.range * light.range, 0.00001f);

                // set direction & extra attentuation info if it's a spot light
                if (light.lightType == LightType.Spot) {
                    Vector4 v = light.localToWorld.GetColumn(2);
                    v.x = -v.x;
                    v.y = -v.y;
                    v.z = -v.z;
                    visibleLightSpotDirections[i] = v;

                    float outerRad = Mathf.Deg2Rad * 0.5f * light.spotAngle;
                    float outerCos = Mathf.Cos(outerRad);
                    float outerTan = Mathf.Tan(outerRad);
                    float innerCos = Mathf.Cos(Mathf.Atan((46.0f/64.0f) * outerTan));
                    float angleRange = Mathf.Max(innerCos - outerCos, 0.001f);
                    a.z = 1.0f / angleRange;
                    a.w = -outerCos * a.z;
                }
            }
            visibleLightAttenuations[i] = a; 
        }

        // let unity know about unused lights so it doesn't write
        //   to out of bounds locations in light data
        if(cullResults.visibleLights.Count > maxVisibleLights) {
            // allocates new memory every frame, booo
            int[] lightIndices = cullResults.GetLightIndexMap();
            for(int i = maxVisibleLights; i < cullResults.visibleLights.Count; i++) {
                lightIndices[i] = -1;
            } 
            cullResults.SetLightIndexMap(lightIndices);
        }
    }

    //------------------------------------------------------------------------------
    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    private void DrawDefaultPipeline (ScriptableRenderContext context, Camera camera) {
        if (errorMaterial == null) {
            Shader errorShader = Shader.Find("Hidden/InternalErrorShader");
            errorMaterial = new Material(errorShader) {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        var drawSettings = new DrawRendererSettings(
            camera, new ShaderPassName("ForwardBase")
        );
        drawSettings.SetShaderPassName(1, new ShaderPassName("PrepassBase"));
		drawSettings.SetShaderPassName(2, new ShaderPassName("Always"));
		drawSettings.SetShaderPassName(3, new ShaderPassName("Vertex"));
		drawSettings.SetShaderPassName(4, new ShaderPassName("VertexLMRGBM"));
		drawSettings.SetShaderPassName(5, new ShaderPassName("VertexLM"));
        drawSettings.SetOverrideMaterial(errorMaterial, 0);

        var filterSettings = new FilterRenderersSettings(true);

        context.DrawRenderers(
            cullResults.visibleRenderers, ref drawSettings, filterSettings
        );
    }
}
