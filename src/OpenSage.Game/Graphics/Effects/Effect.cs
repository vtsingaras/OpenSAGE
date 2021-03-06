﻿using System;
using System.Collections.Generic;
using OpenSage.Graphics.Shaders;
using OpenSage.Utilities.Extensions;
using Veldrid;

namespace OpenSage.Graphics.Effects
{
    public sealed class Effect : DisposableBase
    {
        private static byte _nextID = 0;
        private readonly VertexLayoutDescription[] _vertexDescriptors;
        private readonly Shader _vertexShader;
        private readonly Shader _pixelShader;

        private readonly ResourceLayout[] _resourceLayouts;

        private readonly Dictionary<EffectPipelineStateHandle, Pipeline> _cachedPipelineStates;

        private readonly Dictionary<string, EffectParameter> _parameters;

        private EffectPipelineStateHandle _pipelineStateHandle;
        private Pipeline _pipelineState;

        private EffectDirtyFlags _dirtyFlags;

        public GraphicsDevice GraphicsDevice { get; }
        public byte ID { get; }

        [Flags]
        private enum EffectDirtyFlags
        {
            None = 0,

            PipelineState = 0x1
        }

        public Effect(
            GraphicsDevice graphicsDevice,
            string shaderName,
            params VertexLayoutDescription[] vertexDescriptors)
        {
            GraphicsDevice = graphicsDevice;

            ID = _nextID++;

            var shaderCodeExtension = GetBytecodeExtension(graphicsDevice.BackendType);

            Shader CreateShader(ShaderStages shaderStage, string entryPoint)
            {
                var embeddedResourceName = $"OpenSage.Graphics.Shaders.Compiled.{shaderName}-{shaderStage.ToString().ToLowerInvariant()}{shaderCodeExtension}";
                using (var shaderStream = typeof(Effect).Assembly.GetManifestResourceStream(embeddedResourceName))
                {
                    var shaderBytecode = shaderStream.ReadAllBytes();
                    var shader = graphicsDevice.ResourceFactory.CreateShader(new ShaderDescription(shaderStage, shaderBytecode, entryPoint));
                    shader.Name = shaderName;
                    return shader;
                }
            }

            _vertexShader = AddDisposable(CreateShader(ShaderStages.Vertex, "VS"));
            _pixelShader = AddDisposable(CreateShader(ShaderStages.Fragment, "PS"));

            _cachedPipelineStates = new Dictionary<EffectPipelineStateHandle, Pipeline>();

            _vertexDescriptors = vertexDescriptors;

            var shaderDefinition = ShaderDefinitions.GetShaderDefinition(shaderName);

            _parameters = new Dictionary<string, EffectParameter>();
            _resourceLayouts = new ResourceLayout[shaderDefinition.ResourceBindings.Count];

            for (var i = 0u; i < shaderDefinition.ResourceBindings.Count; i++)
            {
                var resourceBinding = shaderDefinition.ResourceBindings[(int) i];
                var resourceLayoutDescription = new ResourceLayoutElementDescription(
                    resourceBinding.Name,
                    resourceBinding.Type,
                    resourceBinding.Stages);

                var parameter = AddDisposable(new EffectParameter(
                    graphicsDevice,
                    resourceBinding,
                    resourceLayoutDescription,
                    i));

                _parameters[parameter.Name] = parameter;
                _resourceLayouts[i] = parameter.ResourceLayout;
            }
        }

        private static string GetBytecodeExtension(GraphicsBackend backend)
        {
            switch (backend)
            {
                case GraphicsBackend.Direct3D11: return ".hlsl.bytes";
                case GraphicsBackend.Metal: return ".metallib";
                case GraphicsBackend.Vulkan: return ".450.glsl.spv";
                case GraphicsBackend.OpenGL: return ".330.glsl";
                default: throw new InvalidOperationException("Invalid Graphics backend: " + backend);
            }
        }

        internal EffectParameter GetParameter(string name, bool throwIfMissing = true)
        {
            if (!_parameters.TryGetValue(name, out var result))
            {
                if (throwIfMissing)
                {
                    throw new InvalidOperationException($"Could not find parameter with name '{name}'.");
                }
                else
                {
                    return null;
                }
            }
            return result;
        }

        public void Begin(CommandList commandEncoder)
        {
            _dirtyFlags |= EffectDirtyFlags.PipelineState;

            foreach (var parameter in _parameters.Values)
            {
                parameter.SetDirty();
            }
        }

        public void ApplyPipelineState(CommandList commandEncoder)
        {
            if (_dirtyFlags.HasFlag(EffectDirtyFlags.PipelineState))
            {
                commandEncoder.SetPipeline(_pipelineState);

                _dirtyFlags &= ~EffectDirtyFlags.PipelineState;
            }
        }

        public void ApplyParameters(CommandList commandEncoder)
        {
            foreach (var parameter in _parameters.Values)
            {
                parameter.ApplyChanges(commandEncoder);
            }
        }

        public void SetPipelineState(EffectPipelineStateHandle pipelineStateHandle)
        {
            if (_pipelineStateHandle == pipelineStateHandle)
            {
                return;
            }

            _pipelineStateHandle = pipelineStateHandle;
            _pipelineState = GetPipelineState(pipelineStateHandle);
            _dirtyFlags |= EffectDirtyFlags.PipelineState;
        }

        private Pipeline GetPipelineState(EffectPipelineStateHandle pipelineStateHandle)
        {
            if (!_cachedPipelineStates.TryGetValue(pipelineStateHandle, out var result))
            {
                var description = new GraphicsPipelineDescription(
                    pipelineStateHandle.EffectPipelineState.BlendState,
                    pipelineStateHandle.EffectPipelineState.DepthStencilState,
                    pipelineStateHandle.EffectPipelineState.RasterizerState,
                    PrimitiveTopology.TriangleList,
                    new ShaderSetDescription(
                        _vertexDescriptors,
                        new[] { _vertexShader, _pixelShader }),
                    _resourceLayouts,
                    pipelineStateHandle.EffectPipelineState.OutputDescription);

                _cachedPipelineStates[pipelineStateHandle] = result = AddDisposable(GraphicsDevice.ResourceFactory.CreateGraphicsPipeline(ref description));
            }

            return result;
        }
    }
}
