/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using System.Text.Json;
using Xunit;

namespace GadgetExplorer.Tests.Configuration
{
    public sealed class SinkConfigurationBehaviorTests
    {
        [Fact]
        public void Missing_sink_configuration_file_throws()
        {
            var missingPath = Path.Combine(Path.GetTempPath(), $"missing-sinks-{Guid.NewGuid():N}.json");
            Assert.False(File.Exists(missingPath));

            var ex = Assert.Throws<InvalidOperationException>(() => SinkConfigurations.Load(SinkConfigurationKind.Include, missingPath));

            Assert.Contains("Sink config file or directory was not found", ex.Message, StringComparison.Ordinal);
            Assert.Contains(missingPath, ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Missing_sink_configuration_directory_throws()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            string missingDirectoryPath = tempDirectory.GetPath("missing-sinks");
            Assert.False(Directory.Exists(missingDirectoryPath));

            var ex = Assert.Throws<InvalidOperationException>(() => SinkConfigurations.Load(SinkConfigurationKind.Include, missingDirectoryPath));

            Assert.Contains("Sink config file or directory was not found", ex.Message, StringComparison.Ordinal);
            Assert.Contains(missingDirectoryPath, ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Sink_configuration_requires_at_least_one_sink_entry()
        {
            var configPath = Path.Combine(Path.GetTempPath(), $"GadgetExplorer-sinks-{Guid.NewGuid():N}.json");

            try
            {
                File.WriteAllText(
                    configPath,
                    """
                {
                  "sinks": []
                }
                """);

                var ex = Assert.Throws<InvalidOperationException>(() => SinkConfigurations.Load(SinkConfigurationKind.Include, configPath));
                Assert.Contains("does not contain any sinks", ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
        }

        [Fact]
        public void Sink_configuration_loads_constant_argument_filters()
        {
            var configPath = Path.Combine(Path.GetTempPath(), $"GadgetExplorer-sinks-{Guid.NewGuid():N}.json");

            try
            {
                File.WriteAllText(
                    configPath,
                    """
                {
                  "sinks": [
                    {
                      "declaringType": "System.Reflection.Assembly",
                      "methodName": "LoadFrom",
                      "parameters": [
                        {
                          "typeName": "System.String",
                          "ignoreSinkIfConstant": true
                        }
                      ]
                    }
                  ]
                }
                """);

                var sink = Assert.Single(SinkConfigurations.Load(SinkConfigurationKind.Include, configPath));
                Assert.Equal("System.Reflection.Assembly", sink.DeclaringType);
                Assert.Equal("LoadFrom", sink.MethodName);
                Assert.Equal(["System.String"], sink.ParameterTypeNames);
                var parameter = Assert.Single(sink.Parameters);
                Assert.Equal("System.String", parameter.TypeName);
                Assert.True(parameter.IgnoreSinkIfConstant);
            }
            finally
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
        }

        [Fact]
        public void Sink_configuration_loads_per_parameter_constant_filters()
        {
            var configPath = Path.Combine(Path.GetTempPath(), $"GadgetExplorer-sinks-{Guid.NewGuid():N}.json");

            try
            {
                File.WriteAllText(
                    configPath,
                    """
                {
                  "sinks": [
                    {
                      "declaringType": "System.Reflection.Assembly",
                      "methodName": "LoadFrom",
                      "parameters": [
                        {
                          "typeName": "System.String",
                          "ignoreSinkIfConstant": true
                        },
                        {
                          "typeName": "System.Byte[]",
                          "ignoreSinkIfConstant": false
                        }
                      ]
                    }
                  ]
                }
                """);

                var sink = Assert.Single(SinkConfigurations.Load(SinkConfigurationKind.Include, configPath));
                Assert.Equal(["System.String", "System.Byte[]"], sink.ParameterTypeNames);
                Assert.Collection(
                    sink.Parameters,
                    parameter =>
                    {
                        Assert.Equal("System.String", parameter.TypeName);
                        Assert.True(parameter.IgnoreSinkIfConstant);
                    },
                    parameter =>
                    {
                        Assert.Equal("System.Byte[]", parameter.TypeName);
                        Assert.False(parameter.IgnoreSinkIfConstant);
                    });
            }
            finally
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
        }

        [Fact]
        public void Sink_configuration_treats_explicit_empty_parameters_as_parameterless_signature()
        {
            var configPath = Path.Combine(Path.GetTempPath(), $"GadgetExplorer-sinks-{Guid.NewGuid():N}.json");

            try
            {
                File.WriteAllText(
                    configPath,
                    """
                {
                  "sinks": [
                    {
                      "declaringType": "System.Diagnostics.StackTrace",
                      "methodName": ".ctor",
                      "parameters": []
                    }
                  ]
                }
                """);

                var sink = Assert.Single(SinkConfigurations.Load(SinkConfigurationKind.Include, configPath));
                Assert.True(sink.HasExplicitParameterSignature);
                Assert.Empty(sink.Parameters);
                Assert.Empty(sink.ParameterTypeNames);
            }
            finally
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
        }

        [Fact]
        public void Sink_configuration_directory_loads_multiple_files_in_deterministic_file_name_order()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            tempDirectory.WriteFile(
                "b.sinks.json",
                """
            {
              "sinks": [
                {
                  "declaringType": "Example",
                  "methodName": "Second"
                }
              ]
            }
            """);
            tempDirectory.WriteFile(
                "a.sinks.json",
                """
            {
              "sinks": [
                {
                  "declaringType": "Example",
                  "methodName": "First"
                }
              ]
            }
            """);
            tempDirectory.WriteFile(
                Path.Combine("nested", "c.sinks.json"),
                """
            {
              "sinks": [
                {
                  "declaringType": "Example",
                  "methodName": "Nested"
                }
              ]
            }
            """);

            IReadOnlyList<SinkDefinition> sinks = SinkConfigurations.Load(SinkConfigurationKind.Include, tempDirectory.Path);

            Assert.Collection(
                sinks,
                sink => Assert.Equal("First", sink.MethodName),
                sink => Assert.Equal("Second", sink.MethodName));
        }

        [Fact]
        public void Sink_configuration_directory_preserves_signature_shapes_and_constant_filters()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            tempDirectory.WriteFile(
                "a.sinks.json",
                """
            {
              "sinks": [
                {
                  "declaringType": "System.IO.File",
                  "methodName": "Delete"
                }
              ]
            }
            """);
            tempDirectory.WriteFile(
                "b.sinks.json",
                """
            {
              "sinks": [
                {
                  "declaringType": "System.Diagnostics.StackTrace",
                  "methodName": ".ctor",
                  "parameters": []
                }
              ]
            }
            """);
            tempDirectory.WriteFile(
                "c.sinks.json",
                """
            {
              "sinks": [
                {
                  "declaringType": "System.Reflection.Assembly",
                  "methodName": "LoadFrom",
                  "parameters": [
                    {
                      "typeName": "System.String",
                      "ignoreSinkIfConstant": true
                    },
                    {
                      "typeName": "System.Byte[]",
                      "ignoreSinkIfConstant": false
                    }
                  ]
                }
              ]
            }
            """);

            IReadOnlyList<SinkDefinition> sinks = SinkConfigurations.Load(SinkConfigurationKind.Include, tempDirectory.Path);

            Assert.Collection(
                sinks,
                sink =>
                {
                    Assert.Equal("Delete", sink.MethodName);
                    Assert.False(sink.HasExplicitParameterSignature);
                    Assert.Empty(sink.Parameters);
                },
                sink =>
                {
                    Assert.Equal(".ctor", sink.MethodName);
                    Assert.True(sink.HasExplicitParameterSignature);
                    Assert.Empty(sink.Parameters);
                },
                sink =>
                {
                    Assert.Equal("LoadFrom", sink.MethodName);
                    Assert.True(sink.HasExplicitParameterSignature);
                    Assert.Collection(
                        sink.Parameters,
                        parameter =>
                        {
                            Assert.Equal("System.String", parameter.TypeName);
                            Assert.True(parameter.IgnoreSinkIfConstant);
                        },
                        parameter =>
                        {
                            Assert.Equal("System.Byte[]", parameter.TypeName);
                            Assert.False(parameter.IgnoreSinkIfConstant);
                        });
                });
        }

        [Fact]
        public void Sink_configuration_treats_omitted_parameters_as_broad_overload_matching()
        {
            var configPath = Path.Combine(Path.GetTempPath(), $"GadgetExplorer-sinks-{Guid.NewGuid():N}.json");

            try
            {
                File.WriteAllText(
                    configPath,
                    """
                {
                  "sinks": [
                    {
                      "declaringType": "System.IO.File",
                      "methodName": "Delete"
                    }
                  ]
                }
                """);

                var sink = Assert.Single(SinkConfigurations.Load(SinkConfigurationKind.Include, configPath));
                Assert.False(sink.HasExplicitParameterSignature);
                Assert.Empty(sink.Parameters);
                Assert.Empty(sink.ParameterTypeNames);
            }
            finally
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
        }

        [Fact]
        public void Sink_configuration_rejects_removed_first_argument_constant_flag()
        {
            var configPath = Path.Combine(Path.GetTempPath(), $"GadgetExplorer-sinks-{Guid.NewGuid():N}.json");

            try
            {
                File.WriteAllText(
                    configPath,
                    """
                {
                  "sinks": [
                    {
                      "declaringType": "System.Reflection.Assembly",
                      "methodName": "LoadFrom",
                      "parameterTypeNames": [ "System.String" ],
                      "ignoreIfFirstArgumentConstant": true
                    }
                  ]
                }
                """);

                Assert.Throws<JsonException>(() => SinkConfigurations.Load(SinkConfigurationKind.Include, configPath));
            }
            finally
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
        }

        [Fact]
        public void Sink_configuration_loads_native_import_filters()
        {
            var configPath = Path.Combine(Path.GetTempPath(), $"GadgetExplorer-sinks-{Guid.NewGuid():N}.json");

            try
            {
                File.WriteAllText(
                    configPath,
                    """
                {
                  "sinks": [
                    {
                      "nativeModule": "kernel32.dll",
                      "nativeEntryPoint": "LoadLibraryEx*",
                      "parameters": [
                        {
                          "typeName": "System.String",
                          "ignoreSinkIfConstant": false
                        },
                        {
                          "typeName": "System.IntPtr",
                          "ignoreSinkIfConstant": false
                        },
                        {
                          "typeName": "System.UInt32",
                          "ignoreSinkIfConstant": false
                        }
                      ]
                    }
                  ]
                }
                """);

                var sink = Assert.Single(SinkConfigurations.Load(SinkConfigurationKind.Include, configPath));
                Assert.Equal(string.Empty, sink.DeclaringType);
                Assert.Equal(string.Empty, sink.MethodName);
                Assert.Equal("kernel32.dll", sink.NativeModule);
                Assert.Equal("LoadLibraryEx*", sink.NativeEntryPoint);
                Assert.Equal(["System.String", "System.IntPtr", "System.UInt32"], sink.ParameterTypeNames);
            }
            finally
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
        }

        [Fact]
        public void Sink_configuration_rejects_entries_without_declaring_type_and_method_name()
        {
            var configPath = Path.Combine(Path.GetTempPath(), $"GadgetExplorer-sinks-{Guid.NewGuid():N}.json");

            try
            {
                File.WriteAllText(
                    configPath,
                    """
                {
                  "sinks": [
                    {
                      "declaringType": " ",
                      "methodName": "\t"
                    }
                  ]
                }
                """);

                var ex = Assert.Throws<InvalidOperationException>(() => SinkConfigurations.Load(SinkConfigurationKind.Include, configPath));
                Assert.Contains("contains a sink with no declaringType, methodName, nativeModule, or nativeEntryPoint", ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
        }

        [Fact]
        public void Sink_configuration_rejects_explicit_signatures_without_method_names()
        {
            var configPath = Path.Combine(Path.GetTempPath(), $"GadgetExplorer-sinks-{Guid.NewGuid():N}.json");

            try
            {
                File.WriteAllText(
                    configPath,
                    """
                {
                  "sinks": [
                    {
                      "declaringType": "System.Reflection.Assembly",
                      "parameterTypeNames": [ "System.String" ]
                    }
                  ]
                }
                """);

                var ex = Assert.Throws<InvalidOperationException>(() => SinkConfigurations.Load(SinkConfigurationKind.Include, configPath));
                Assert.Contains("contains a sink with parameterTypeNames but no methodName or nativeEntryPoint", ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
        }

        [Fact]
        public void Sink_configuration_rejects_mixed_parameter_formats()
        {
            var configPath = Path.Combine(Path.GetTempPath(), $"GadgetExplorer-sinks-{Guid.NewGuid():N}.json");

            try
            {
                File.WriteAllText(
                    configPath,
                    """
                {
                  "sinks": [
                    {
                      "declaringType": "System.Reflection.Assembly",
                      "methodName": "LoadFrom",
                      "parameterTypeNames": [ "System.String" ],
                      "parameters": [
                        {
                          "typeName": "System.String",
                          "ignoreSinkIfConstant": true
                        }
                      ]
                    }
                  ]
                }
                """);

                var ex = Assert.Throws<InvalidOperationException>(() => SinkConfigurations.Load(SinkConfigurationKind.Include, configPath));
                Assert.Contains("mixes the parameters format with parameterTypeNames", ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
        }

        [Fact]
        public void Sink_configuration_rejects_blank_parameter_type_names()
        {
            var configPath = Path.Combine(Path.GetTempPath(), $"GadgetExplorer-sinks-{Guid.NewGuid():N}.json");

            try
            {
                File.WriteAllText(
                    configPath,
                    """
                {
                  "sinks": [
                    {
                      "declaringType": "System.Reflection.Assembly",
                      "methodName": "LoadFrom",
                      "parameters": [
                        {
                          "typeName": " "
                        }
                      ]
                    }
                  ]
                }
                """);

                var ex = Assert.Throws<InvalidOperationException>(() => SinkConfigurations.Load(SinkConfigurationKind.Include, configPath));
                Assert.Contains("contains a sink parameter with no typeName", ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
        }

        [Fact]
        public void Parameter_type_name_format_defaults_ignore_sink_if_constant_to_false()
        {
            var configPath = Path.Combine(Path.GetTempPath(), $"GadgetExplorer-sinks-{Guid.NewGuid():N}.json");

            try
            {
                File.WriteAllText(
                    configPath,
                    """
                {
                  "sinks": [
                    {
                      "declaringType": "System.Reflection.Assembly",
                      "methodName": "LoadFrom",
                      "parameterTypeNames": [ "System.String", "System.Byte[]" ]
                    }
                  ]
                }
                """);

                var sink = Assert.Single(SinkConfigurations.Load(SinkConfigurationKind.Include, configPath));
                Assert.Collection(
                    sink.Parameters,
                    parameter =>
                    {
                        Assert.Equal("System.String", parameter.TypeName);
                        Assert.False(parameter.IgnoreSinkIfConstant);
                    },
                    parameter =>
                    {
                        Assert.Equal("System.Byte[]", parameter.TypeName);
                        Assert.False(parameter.IgnoreSinkIfConstant);
                    });
            }
            finally
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
        }
    }
}

