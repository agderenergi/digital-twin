# DigitalTwinsService
C# service for model and twin management for Azure Digital Twins
Contains a service with generic Azure Digital Twins-related funcionality and conversion to/from C# Classes

DigitalTwinsService.cs is split into the following regions:
* Create Client (with two possible types of credentials, local Azure sign-in or with ClientId/Secret)
* Model Management (validation and CRUD-operations for DTDL models)
* Twin Management (validation and CRUD-operations for twin instances, including some simple queries and use of C# Classes with custom attributes)
* Helper Methods (the actual parsing between ADT basic helper classes and C# Classes)

DigitalTwinsException.cs, DTRelationship.cs, DTmodelAttribute.cs and DTModelContentAttribute.cs are used by DigitalTwinsService.cs.

The repo contains a console app and "ServiceTester.cs" for testing and experimentation. Some example DTDL Models and C# Classes that illustrates the use of the custom attributes are also included.
