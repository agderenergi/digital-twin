# DigitalTwinsService
C# service for model and twin management for Azure Digital Twins

Includes some test models and C# classes and a ServiceTester class for experimentation. An appsettings.local.json with the Digital Twins instance URL set is expected. Set "UseLocalAzureSignIn" true for easy authentication when running locally (I had to sign out and back in in the Azure Portal once for it to work). Without LocalAzureSignIn, a client ID and Secret is needed.
