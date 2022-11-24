﻿using System;
using System.Collections.Generic;
using Fuse.compute;
using System.Linq;
using System.Text;
using VL.Core;

namespace Fuse.regions
{
    public class IfGroup : Group
    {
        private readonly ShaderNode<bool> _inCheck;

        public IfGroup(
            NodeContext nodeContext,
            ShaderNode<bool> inCheck,
            IEnumerable<AbstractShaderNode> theInputs) : base(nodeContext, "IfGroup")
        {
            Name = "IfGroup";
            _inCheck = inCheck;

            SetInputs(theInputs);
        }

        protected internal override void BuildSource(StringBuilder theSourceBuilder, HashSet<uint> theHashes)
        {
            if (!theHashes.Add(HashCode)) return;

            const string shaderCode = @"
        if(${check}){";
            theSourceBuilder.Append(
                ShaderNodesUtil.Evaluate(
                    shaderCode,
                    new Dictionary<string, string> {{"check", _inCheck.ID}}
                )
            );
            theSourceBuilder.AppendLine();
            var myChildSourceBuilder = new StringBuilder();
            BuildChildrenSource(myChildSourceBuilder, theHashes);
            var myChildSource = myChildSourceBuilder.ToString();
            myChildSource = ShaderNodesUtil.IndentCode(myChildSource);
            theSourceBuilder.Append(myChildSource);

            var source = SourceCode;
            if (!string.IsNullOrWhiteSpace(source.Trim()) && theHashes.Add(HashCode))
            {
                theSourceBuilder.Append("        " + source + Environment.NewLine);
            }

            theSourceBuilder.Append(@"
        }");
            //   theSourceBuilder.AppendLine();
        }
    }

    public class IfRegion : ShaderNode<GpuVoid>
    {

        public IfRegion(NodeContext nodeContext) : base(nodeContext, "ifRegion")
        {
            OptionalOutputs = new List<AbstractShaderNode>();
        }

        public void Setup(
            ShaderNode<bool> inCheck,
            IEnumerable<AbstractShaderNode> theInputs,
            IEnumerable<AbstractShaderNode> theOutputs,
            IEnumerable<AbstractShaderNode> theCrossLinks)
        {
            var subContextFactory = new NodeSubContextFactory(NodeContext);
            OptionalOutputs.Clear();

            var inputs = theInputs.ToList();
            var outputs = theOutputs.ToList();
            var crossLinks = theCrossLinks.ToList();
            
            if (inputs.Count != outputs.Count)
            {
                SetInputs(inputs);
                return;
            }

            var myCrossLinks = new List<AbstractShaderNode>();
            crossLinks.ForEach(c =>
            {
                if (c == null) return;
                myCrossLinks.Add(c);
            });

            var myInputs = new List<AbstractShaderNode>();
            var myOutputs = new List<AbstractShaderNode>();

            for (var i = 0; i < outputs.Count; i++)
            {
                switch (outputs[i])
                {
                    case ShaderNode<GpuVoid> shaderNode:
                        var myInputVoid = inputs[i];
                        myInputs.Add(myInputVoid);
                        myOutputs.Add(shaderNode);

                        break;
                    default:
                        var myInput = i >= inputs.Count || inputs[i] == null ? AbstractCreation.AbstractConstant(subContextFactory, outputs[i], 0f) : inputs[i];
                        var myDeclareValue = AbstractCreation.AbstractDeclareValueAssigned(subContextFactory, myInput);
                        myInputs.Add(myDeclareValue);

                        var myOutput = i >= outputs.Count
                            ? AbstractCreation.AbstractConstant(subContextFactory, inputs[i], 0f)
                            : outputs[i];
                        var myAssign = AbstractCreation.AbstractAssignNode(subContextFactory, myDeclareValue, myOutput);
                        myOutputs.Add(myAssign);

                        break;
                }
            }

            var inCall = new Group(subContextFactory.NextSubContext());
            inCall.SetInput(myInputs);

            var crossLinkCall = new Group(subContextFactory.NextSubContext());
            crossLinkCall.SetInput(myCrossLinks);

            for (var i = 0; i < outputs.Count; i++)
            {
                switch (outputs[i])
                {
                    case ShaderNode<GpuVoid> shaderNode:
                        var myInputVoid = myInputs[i] ?? new EmptyVoid(subContextFactory.NextSubContext());
                        var outputVoid = AbstractCreation.AbstractOutput(subContextFactory, this, myInputVoid);
                        OptionalOutputs.Add(outputVoid);

                        break;
                    default:
                        var myInput = myInputs[i] ?? AbstractCreation.AbstractConstant(subContextFactory, outputs[i],0);
                        var output = AbstractCreation.AbstractOutput(subContextFactory, this, myInput);
                        OptionalOutputs.Add(output);

                        break;
                }
            }

            ShaderNode<GpuVoid> ifGroup = new IfGroup(subContextFactory.NextSubContext(), inCheck, myOutputs);

            var inputList = new List<AbstractShaderNode>
            {
                crossLinkCall,
                inCheck,
                inCall,
                ifGroup
            };
            SetInputs(inputList);
        }

        protected internal override void BuildSource(StringBuilder theSourceBuilder, HashSet<uint> theHashes)
        {
            var nodes = new List<AbstractShaderNode>();
            Children.ForEach(child =>
            {
                if (!(child is ShaderTree input))
                {
                    return;
                }

                nodes.Add(input.Node);
                input.Node.BuildSource(theSourceBuilder, theHashes);
            });


            var source = SourceCode;
            if (!string.IsNullOrWhiteSpace(source) && theHashes.Add(HashCode))
            {
                theSourceBuilder.Append("        " + source + Environment.NewLine);
            }
        }
    }
}