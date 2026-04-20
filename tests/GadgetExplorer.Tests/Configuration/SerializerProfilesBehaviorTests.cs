/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit;

namespace GadgetExplorer.Tests.Configuration
{
    public sealed class SerializerProfilesBehaviorTests
    {
        [Fact]
        public void Unknown_profile_name_throws_with_available_profiles()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => SerializerProfiles.ResolveShipped("DoesNotExist"));

            Assert.Contains("Unknown serializer profile 'DoesNotExist'.", ex.Message, StringComparison.Ordinal);
            Assert.Contains("JsonDotNet", ex.Message, StringComparison.Ordinal);
            Assert.Contains("MessagePackTypeless", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("json-dot-net")]
        [InlineData("json_dot_net")]
        [InlineData("json dot net")]
        [InlineData("json.dot.net")]
        public void Normalized_profile_name_variants_resolve_shipped_profile(string requestedName)
        {
            var profile = SerializerProfiles.ResolveShipped(requestedName);

            Assert.Equal("JsonDotNet", profile.Name);
        }

        [Fact]
        public void Missing_explicit_profile_path_throws()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            var missingPath = tempDirectory.GetPath("missing.profile.json");
            Assert.False(File.Exists(missingPath));

            var ex = Assert.Throws<InvalidOperationException>(() => SerializerProfiles.LoadFromPath(missingPath));

            Assert.Contains("Serializer profile file was not found", ex.Message, StringComparison.Ordinal);
            Assert.Contains(missingPath, ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Profile_file_without_name_is_rejected()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            var profilePath = tempDirectory.WriteFile(
                "BlankName.profile.json",
                """
            {
              "name": "",
              "rootTypeEligibility": {},
              "triggerPolicy": {
                "supportsFinalizers": false,
                "allowedPropertySetterVisibilities": []
              },
              "activationPolicies": [
                {
                  "mode": "ConstructorSelection"
                }
              ],
              "callbacks": {
                "attributeCallbacks": [],
                "interfaceCallbacks": []
              }
            }
            """);

            var ex = Assert.Throws<InvalidOperationException>(() => SerializerProfiles.LoadFromPath(profilePath));

            Assert.Contains("does not define a profile name", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Profile_file_without_activation_paths_is_rejected()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            var profilePath = tempDirectory.WriteFile(
                "NoActivation.profile.json",
                """
            {
              "name": "NoActivation",
              "rootTypeEligibility": {},
              "triggerPolicy": {
                "supportsFinalizers": false,
                "allowedPropertySetterVisibilities": []
              },
              "activationPolicies": [],
              "callbacks": {
                "attributeCallbacks": [],
                "interfaceCallbacks": []
              }
            }
            """);

            var ex = Assert.Throws<InvalidOperationException>(() => SerializerProfiles.LoadFromPath(profilePath));

            Assert.Contains("does not define any activation policies", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Profile_file_without_explicit_property_setter_visibilities_defaults_to_empty_list()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            var profilePath = tempDirectory.WriteFile(
                "MissingSetterVisibility.profile.json",
                """
            {
              "name": "MissingSetterVisibility",
              "rootTypeEligibility": {},
              "triggerPolicy": {
                "supportsFinalizers": false
              },
              "activationPolicies": [
                {
                  "mode": "ConstructorSelection"
                }
              ],
              "callbacks": {
                "attributeCallbacks": [],
                "interfaceCallbacks": []
              }
            }
            """);

            var profile = SerializerProfiles.LoadFromPath(profilePath);

            Assert.Empty(profile.AllowedPropertySetterVisibilities);
            Assert.False(profile.SupportsPublicPropertySetterTriggers);
        }

        [Fact]
        public void Profile_file_without_explicit_property_getter_visibilities_defaults_to_empty_list()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            var profilePath = tempDirectory.WriteFile(
                "MissingGetterVisibility.profile.json",
                """
            {
              "name": "MissingGetterVisibility",
              "rootTypeEligibility": {},
              "triggerPolicy": {
                "supportsFinalizers": false
              },
              "activationPolicies": [
                {
                  "mode": "ConstructorSelection"
                }
              ],
              "callbacks": {
                "attributeCallbacks": [],
                "interfaceCallbacks": []
              }
            }
            """);

            var profile = SerializerProfiles.LoadFromPath(profilePath);

            Assert.Empty(profile.AllowedPropertyGetterVisibilities);
            Assert.False(profile.SupportsPublicPropertyGetterTriggers);
        }

        [Fact]
        public void Serialization_constructor_profile_without_visibility_policy_is_rejected()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            var profilePath = tempDirectory.WriteFile(
                "MissingSerializationConstructorVisibility.profile.json",
                """
            {
              "name": "MissingSerializationConstructorVisibility",
              "rootTypeEligibility": {},
              "triggerPolicy": {
                "supportsFinalizers": false,
                "allowedPropertySetterVisibilities": []
              },
              "activationPolicies": [
                {
                  "mode": "SerializationConstructor",
                  "requiredDeclaringTypeInterfaceNames": [
                    "System.Runtime.Serialization.ISerializable"
                  ],
                  "serializationConstructorSignature": {
                    "parameterTypeNames": [
                      "System.Runtime.Serialization.SerializationInfo",
                      "System.Runtime.Serialization.StreamingContext"
                    ]
                  }
                }
              ],
              "callbacks": {
                "attributeCallbacks": [],
                "interfaceCallbacks": []
              }
            }
            """);

            var ex = Assert.Throws<InvalidOperationException>(() => SerializerProfiles.LoadFromPath(profilePath));

            Assert.Contains("must define activationPolicies[].serializationConstructorSignature.visibilityPolicy", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Available_profile_names_are_sorted_and_distinct()
        {
            var availableNames = SerializerProfiles.GetAvailableProfileNames();

            Assert.Equal(
                availableNames.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                availableNames);
            Assert.Equal(availableNames.Count, availableNames.Distinct(StringComparer.Ordinal).Count());
            Assert.Contains("MessagePackTypeless", availableNames);
        }

        [Fact]
        public void Profile_file_can_define_activation_specific_requirements_and_non_public_setter_opt_in()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            var profilePath = tempDirectory.WriteFile(
                "JsonLike.profile.json",
                """
            {
              "name": "JsonLike",
              "rootTypeEligibility": {
                "allowedTypeVisibilities": [
                  "PubliclyVisible"
                ]
              },
              "triggerPolicy": {
                "supportsFinalizers": false,
                "allowedPropertySetterVisibilities": [
                  "Public"
                ],
                "nonPublicPropertySetterOptInAttributeTypeNames": [
                  "Newtonsoft.Json.JsonPropertyAttribute"
                ]
              },
              "activationPolicies": [
                {
                  "mode": "SerializationConstructor",
                  "requiredDeclaringTypeInterfaceNames": [
                    "System.Runtime.Serialization.ISerializable"
                  ],
                  "serializationConstructorSignature": {
                    "visibilityPolicy": {
                      "sealedTypeAllowedVisibilities": [
                        "Private"
                      ],
                      "unsealedTypeAllowedVisibilities": [
                        "Family"
                      ]
                    },
                    "parameterTypeNames": [
                      "System.Runtime.Serialization.SerializationInfo",
                      "System.Runtime.Serialization.StreamingContext"
                    ]
                  },
                  "requirements": [
                    {
                      "kind": "HasAttribute",
                      "typeName": "System.SerializableAttribute"
                    }
                  ]
                }
              ],
              "callbacks": {
                "attributeCallbacks": [],
                "interfaceCallbacks": []
              }
            }
            """);

            var profile = SerializerProfiles.LoadFromPath(profilePath);
            var activationPolicy = Assert.Single(profile.ActivationPolicies);

            Assert.Equal("JsonLike", profile.Name);
            Assert.Equal([MethodVisibility.Public], profile.AllowedPropertySetterVisibilities);
            Assert.Contains("Newtonsoft.Json.JsonPropertyAttribute", profile.NonPublicPropertySetterOptInAttributeTypeNames);
            Assert.Equal(ActivationMode.SerializationConstructor, activationPolicy.Mode);
            Assert.Contains("System.Runtime.Serialization.ISerializable", activationPolicy.RequiredDeclaringTypeInterfaceNames);
            Assert.Equal([MethodVisibility.Private], activationPolicy.SerializationConstructorSignature!.VisibilityPolicy.SealedTypeAllowedVisibilities);
            Assert.Equal([MethodVisibility.Family], activationPolicy.SerializationConstructorSignature.VisibilityPolicy.UnsealedTypeAllowedVisibilities);
            Assert.Contains(activationPolicy.Requirements, requirement =>
                requirement is { Kind: RootTypeRequirementKind.HasAttribute, TypeName: "System.SerializableAttribute" });
        }

        [Fact]
        public void Profile_file_can_define_property_getter_visibilities()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            var profilePath = tempDirectory.WriteFile(
                "GetterLike.profile.json",
                """
            {
              "name": "GetterLike",
              "rootTypeEligibility": {
                "allowedTypeVisibilities": [
                  "PubliclyVisible"
                ]
              },
              "triggerPolicy": {
                "supportsFinalizers": false,
                "allowedPropertyGetterVisibilities": [
                  "Public",
                  "Family"
                ],
                "allowedPropertySetterVisibilities": []
              },
              "activationPolicies": [
                {
                  "mode": "ConstructorSelection"
                }
              ],
              "callbacks": {
                "attributeCallbacks": [],
                "interfaceCallbacks": []
              }
            }
            """);

            var profile = SerializerProfiles.LoadFromPath(profilePath);

            Assert.Equal([MethodVisibility.Public, MethodVisibility.Family], profile.AllowedPropertyGetterVisibilities);
            Assert.True(profile.SupportsPublicPropertyGetterTriggers);
        }

        [Fact]
        public void Profile_file_can_define_custom_deserialization_methods_and_ordinary_object_mapping_rules()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            var profilePath = tempDirectory.WriteFile(
                "XmlLike.profile.json",
                """
            {
              "name": "XmlLike",
              "rootTypeEligibility": {
                "allowedTypeVisibilities": [
                  "PubliclyVisible"
                ]
              },
              "triggerPolicy": {
                "supportsFinalizers": true,
                "allowedPropertySetterVisibilities": [
                  "Public"
                ]
              },
              "activationPolicies": [
                {
                  "mode": "ConstructorSelection",
                  "constructorSelectionRules": [
                    {
                      "when": {
                        "publicParameterlessCount": 1
                      },
                      "target": "PublicParameterless"
                    }
                  ],
                  "ordinaryObjectMapping": {
                    "ignoredDeclaringTypeInterfaceNames": [
                      "System.Xml.Serialization.IXmlSerializable"
                    ],
                    "rejectPublicFieldsOrSettablePropertiesOfInterfaceTypes": true,
                    "rejectedPublicFieldOrSettablePropertyTypeNames": [
                      "System.Type"
                    ]
                  }
                }
              ],
              "callbacks": {
                "attributeCallbacks": [],
                "interfaceCallbacks": []
              },
              "customDeserializationMethods": {
                "interfaceMethods": [
                  {
                    "interfaceTypeName": "System.Xml.Serialization.IXmlSerializable",
                    "methodName": "ReadXml",
                    "parameterTypeNames": [
                      "System.Xml.XmlReader"
                    ],
                    "returnTypeName": "System.Void"
                  }
                ]
              }
            }
            """);

            var profile = SerializerProfiles.LoadFromPath(profilePath);
            var activationPolicy = Assert.Single(profile.ActivationPolicies);
            var customMethod = Assert.Single(profile.CustomDeserializationMethods.InterfaceMethods);

            Assert.Equal("XmlLike", profile.Name);
            Assert.True(profile.SupportsCustomDeserializationMethodTriggers);
            Assert.True(activationPolicy.OrdinaryObjectMapping.RejectPublicFieldsOrSettablePropertiesOfInterfaceTypes);
            Assert.Contains("System.Xml.Serialization.IXmlSerializable", activationPolicy.OrdinaryObjectMapping.IgnoredDeclaringTypeInterfaceNames);
            Assert.Equal(["System.Type"], activationPolicy.OrdinaryObjectMapping.RejectedPublicFieldOrSettablePropertyTypeNames);
            Assert.Equal("System.Xml.Serialization.IXmlSerializable", customMethod.InterfaceTypeName);
            Assert.Equal("ReadXml", customMethod.MethodName);
        }

        [Fact]
        public void Profile_file_can_define_explicit_root_visibility_matchers()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            var profilePath = tempDirectory.WriteFile(
                "VisibilityLike.profile.json",
                """
            {
              "name": "VisibilityLike",
              "rootTypeEligibility": {
                "allowedTypeVisibilities": [
                  "PubliclyVisible",
                  "Private"
                ]
              },
              "triggerPolicy": {
                "supportsFinalizers": false,
                "allowedPropertySetterVisibilities": []
              },
              "activationPolicies": [
                {
                  "mode": "ConstructorSelection"
                }
              ],
              "callbacks": {
                "attributeCallbacks": [],
                "interfaceCallbacks": []
              }
            }
            """);

            var profile = SerializerProfiles.LoadFromPath(profilePath);

            Assert.Equal(
                [RootTypeVisibility.PubliclyVisible, RootTypeVisibility.Private],
                profile.AllowedRootTypeVisibilities);
        }

    }
}

