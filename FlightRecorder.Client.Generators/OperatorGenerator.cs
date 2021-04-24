﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace FlightRecorder.Client.Generators
{
    [Generator]
    public class OperatorGenerator : BaseGenerator, ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
#if DEBUG
            if (!Debugger.IsAttached)
            {
                //Debugger.Launch();
            }
#endif
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var fields = GetSimConnectFields(context, AircraftPosition).ToList();
            AddSetStruct(context, fields);
            AddOperator(context, fields);
        }

        private static void AddSetStruct(GeneratorExecutionContext context, List<(string type, string name, string variable, string unit, int dataType, int? setType, string setByEvent, double min, double max)> fields)
        {
            var builder = new StringBuilder();
            builder.Append(@"
using System;
using FlightRecorder.Client;

namespace FlightRecorder.Client
{
    public partial struct AircraftPositionSetStruct
    {");

            foreach ((var type, var name, _, _, _, var setType, _, _, _) in fields)
            {
                if (setType == null || setType == SetTypeDefault)
                {
                    builder.Append($@"
        public {type} {name};");
                }
            }

            builder.Append(@"
    }
}");

            context.AddSource("SetStruct", SourceText.From(builder.ToString(), Encoding.UTF8));
        }

        private static void AddOperator(GeneratorExecutionContext context, List<(string type, string name, string variable, string unit, int dataType, int? setType, string setByEvent, double min, double max)> fields)
        {
            var builder = new StringBuilder();
            builder.Append(@"
using System;
using FlightRecorder.Client;

namespace FlightRecorder.Client
{
    public partial class AircraftPositionStructOperator
    {");

            builder.Append(@"
        public static partial AircraftPositionSetStruct ToSet(AircraftPositionStruct variables)
            => new AircraftPositionSetStruct
            {");
            foreach ((_, var name, _, _, _, var setType, _, _, _) in fields)
            {
                if (setType == null || setType == SetTypeDefault)
                {
                    builder.Append($@"
                {name} = variables.{name},");
                }
            }
            builder.Append(@"
            };
");

            builder.Append(@"
        public static AircraftPositionSetStruct Add(AircraftPositionSetStruct position1, AircraftPositionSetStruct position2)
            => new AircraftPositionSetStruct
            {");
            foreach ((_, var name, _, _, _, var setType, _, _, _) in fields)
            {
                if (setType == null || setType == SetTypeDefault)
                {
                    builder.Append($@"
                {name} = position1.{name} + position2.{name},");
                }
            }
            builder.Append(@"
            };
");

            builder.Append(@"
        public static AircraftPositionSetStruct Scale(AircraftPositionSetStruct position, double factor)
            => new AircraftPositionSetStruct
            {");
            foreach ((var type, var name, _, _, _, var setType, _, _, _) in fields)
            {
                if (setType == null || setType == SetTypeDefault)
                {
                    switch (type)
                    {
                        case "double":
                            builder.Append($@"
                {name} = position.{name} * factor,"); // TODO: support wrapping around for angle
                            break;
                        case "int":
                            builder.Append($@"
                {name} = (int)Math.Round(position.{name} * factor),");
                            break;
                        case "uint":
                            builder.Append($@"
                {name} = (uint)Math.Round(position.{name} * factor),");
                            break;
                        default:
                            // TODO: warning
                            break;
                    }
                }
            }
            builder.Append(@"
            };
");

            builder.Append(@"
        public static AircraftPositionSetStruct Interpolate(AircraftPositionSetStruct position1, AircraftPositionSetStruct position2, double interpolation)
            => new AircraftPositionSetStruct
            {");
            foreach ((var type, var name, _, _, _, var setType, _, var min, var max) in fields)
            {
                if (setType == null || setType == SetTypeDefault)
                {
                    switch (type)
                    {
                        case "double":
                            if (min < max)
                            {
                                builder.Append($@"
                {name} = InterpolateWrap(position1.{name}, position2.{name}, interpolation, {min}, {max}),");
                            }
                            else
                            {
                                builder.Append($@"
                {name} = position1.{name} * interpolation + position2.{name} * (1 - interpolation),");
                            }
                            break;
                        case "int":
                            builder.Append($@"
                {name} = (int)Math.Round(position1.{name} * interpolation + position2.{name} * (1 - interpolation)),");
                            break;
                        case "uint":
                            builder.Append($@"
                {name} = (uint)Math.Round(position1.{name} * interpolation + position2.{name} * (1 - interpolation)),");
                            break;
                        default:
                            // TODO: warning
                            break;
                    }
                }
            }
            builder.Append(@"
            };
");

            builder.Append(@"
    }
}");

            context.AddSource("OperatorGenerator", SourceText.From(builder.ToString(), Encoding.UTF8));
        }
    }
}
