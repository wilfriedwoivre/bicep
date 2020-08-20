﻿using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bicep.Core.SemanticModel;
using Bicep.Core.Syntax;
using Bicep.LanguageServer.Providers;
using Bicep.LanguageServer.Utils;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Bicep.LanguageServer.Handlers
{
    public class BicepHoverHandler : HoverHandler
    {
        private readonly ISymbolResolver symbolResolver;

        public BicepHoverHandler(ISymbolResolver symbolResolver) : base(CreateRegistrationOptions())
        {
            this.symbolResolver = symbolResolver;
        }

        public override Task<Hover> Handle(HoverParams request, CancellationToken cancellationToken)
        {
            var result = this.symbolResolver.ResolveSymbol(request.TextDocument.Uri, request.Position);
            if (result == null)
            {
                return Task.FromResult(new Hover());
            }

            var markdown = GetMarkdown(result);
            if (markdown == null)
            {
                return Task.FromResult(new Hover());
            }

            return Task.FromResult<Hover>(new Hover
            {
                Contents = new MarkedStringsOrMarkupContent(new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = markdown
                }),
                Range = PositionHelper.GetNameRange(result.Context.LineStarts, result.Origin)
            });
        }

        private static HoverRegistrationOptions CreateRegistrationOptions() =>
            new HoverRegistrationOptions
            {
                DocumentSelector = DocumentSelectorFactory.Create()
            };

        private static string? GetMarkdown(SymbolResolutionResult result)
        {
            // all of the generated markdown includes the language id to avoid VS code rendering 
            // with multiple borders
            switch (result.Symbol)
            {
                case ParameterSymbol parameter:
                    return $"```bicep\nparam {parameter.Name}: {parameter.Type}\n```";

                case VariableSymbol variable:
                    return $"```bicep\nvar {variable.Name}: {variable.Type}\n```";

                case ResourceSymbol resource:
                    return $"```bicep\nresource {resource.Name}\n{resource.Type}\n```";

                case OutputSymbol output:
                    return $"```bicep\noutput {output.Name}: {output.Type}\n```";

                case FunctionSymbol function when result.Origin is FunctionCallSyntax functionCall:
                    // it's not possible for a non-function call syntax to resolve to a function symbol
                    // but this simplifies the checks
                    return GetFunctionMarkdown(function, functionCall, result.Context.Compilation.GetSemanticModel());

                default:
                    return null;
            }
        }

        private static string GetFunctionMarkdown(FunctionSymbol function, FunctionCallSyntax functionCall, SemanticModel model)
        {
            var buffer = new StringBuilder();
            buffer.Append($"```bicep\nfunction ");
            buffer.Append(function.Name);
            buffer.Append('(');

            const string argumentSeparator = ", ";
            foreach (FunctionArgumentSyntax argumentSyntax in functionCall.Arguments)
            {
                var argumentType = model.GetTypeInfo(argumentSyntax);
                buffer.Append(argumentType);
                
                buffer.Append(argumentSeparator);
            }

            // remove trailing argument separator (if any)
            if (functionCall.Arguments.Length > 0)
            {
                buffer.Remove(buffer.Length - argumentSeparator.Length, argumentSeparator.Length);
            }

            buffer.Append("): ");
            buffer.Append(model.GetTypeInfo(functionCall));
            buffer.Append("\n```");

            return buffer.ToString();
        }
    }
}