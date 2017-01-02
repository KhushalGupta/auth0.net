﻿using System;
using System.Threading.Tasks;
using System.Web;
using AngleSharp;
using AngleSharp.Dom.Html;
using AngleSharp.Extensions;
using Auth0.AuthenticationApi.Models;
using Auth0.Core;
using Auth0.ManagementApi;
using Auth0.ManagementApi.Models;
using Auth0.Tests.Shared;
using FluentAssertions;
using NUnit.Framework;

namespace Auth0.AuthenticationApi.IntegrationTests
{
    [TestFixture]
    public class AuthenticationTests : TestBase
    {
        private ManagementApiClient managementApiClient;
        private Connection connection;
        private User user;
        private User plusUser;
        private User userInDefaultDirectory;

        [SetUp]
        public async Task SetUp()
        {
            var scopes = new
            {
                users = new
                {
                    actions = new string[] { "create", "delete" }
                },
                connections = new
                {
                    actions = new string[] { "create", "delete" }
                },
                tenant_settings = new
                {
                    actions = new string[] { "read" }
                }
            };
            string token = GenerateToken(scopes);

            managementApiClient = new ManagementApiClient(token, new Uri(GetVariable("AUTH0_MANAGEMENT_API_URL")));

            var tenantSettings = await managementApiClient.TenantSettings.GetAsync();

            if (string.IsNullOrEmpty(tenantSettings.DefaultDirectory))
            {
                Assert.Fail("Tests require a tenant with a Default Directory selected.\r\n" +
                    "Enable OAuth 2.0 API Authorization under Account Settings | General and "+
                    "select a Default Directory under Account Settings | General");
            }
            
            // We will need a connection to add the users to...
            connection = await managementApiClient.Connections.CreateAsync(new ConnectionCreateRequest
            {
                Name = Guid.NewGuid().ToString("N"),
                Strategy = "auth0",
                EnabledClients = new []{ GetVariable("AUTH0_CLIENT_ID") }
            });

            // And add a dummy user to test against
            user = await managementApiClient.Users.CreateAsync(new UserCreateRequest
            {
                Connection = connection.Name,
                Email = $"{Guid.NewGuid().ToString("N")}@nonexistingdomain.aaa",
                EmailVerified = true,
                Password = "password"
            });

            // Add a user with a + in the username
            plusUser = await managementApiClient.Users.CreateAsync(new UserCreateRequest
            {
                Connection = connection.Name,
                Email = $"{Guid.NewGuid().ToString("N")}+1@nonexistingdomain.aaa",
                EmailVerified = true,
                Password = "password"
            });

            // Add a user with a + in the username
            userInDefaultDirectory = await managementApiClient.Users.CreateAsync(new UserCreateRequest
            {
                Connection = tenantSettings.DefaultDirectory,
                Email = $"{Guid.NewGuid().ToString("N")}+1@nonexistingdomain.aaa",
                EmailVerified = true,
                Password = "password"
            });
        }

        [TearDown]
        public async Task TearDown()
        {
            await managementApiClient.Users.DeleteAsync(user.UserId);
            await managementApiClient.Users.DeleteAsync(userInDefaultDirectory.UserId);
            await managementApiClient.Users.DeleteAsync(plusUser.UserId);
            await managementApiClient.Connections.DeleteAsync(connection.Id);
        }

        [Test]
        public async Task Can_authenticate_against_Auth0()
        {
            // Arrange
            var authenticationApiClient = new AuthenticationApiClient(new Uri(GetVariable("AUTH0_AUTHENTICATION_API_URL")));

            // Act
            var authenticationResponse = await authenticationApiClient.AuthenticateAsync(new AuthenticationRequest
            {
                ClientId = GetVariable("AUTH0_CLIENT_ID"),
                ClientSecret = GetVariable("AUTH0_CLIENT_SECRET"),
                Connection = connection.Name,
                GrantType = "password",
                Scope = "openid",
                Username = user.Email,
                Password = "password"
                
            });

            // Assert
            authenticationResponse.Should().NotBeNull();
            authenticationResponse.TokenType.Should().NotBeNull();
            authenticationResponse.AccessToken.Should().NotBeNull();
            authenticationResponse.IdToken.Should().NotBeNull();
            authenticationResponse.RefreshToken.Should().BeNull(); // No refresh token if offline access was not requested
        }

        [Test]
        public async Task Can_authenticate_to_default_directory()
        {
            // Arrange
            var authenticationApiClient = new AuthenticationApiClient(new Uri(GetVariable("AUTH0_AUTHENTICATION_API_URL")));

            // Act
            var authenticationResponse = await authenticationApiClient.AuthenticateAsync(new AuthenticationRequest
            {
                ClientId = GetVariable("AUTH0_CLIENT_ID"),
                ClientSecret = GetVariable("AUTH0_CLIENT_SECRET"),
                GrantType = "password",
                Scope = "openid",
                Username = userInDefaultDirectory.UserName,
                Password = "password"

            });

            // Assert
            authenticationResponse.Should().NotBeNull();
            authenticationResponse.TokenType.Should().NotBeNull();
            authenticationResponse.AccessToken.Should().NotBeNull();
            authenticationResponse.IdToken.Should().NotBeNull();
            authenticationResponse.RefreshToken.Should().BeNull(); // No refresh token if offline access was not requested
        }

        [Test]
        public async Task Can_request_offline_access()
        {
            // Arrange
            var authenticationApiClient = new AuthenticationApiClient(new Uri(GetVariable("AUTH0_AUTHENTICATION_API_URL")));

            // Act
            var authenticationResponse = await authenticationApiClient.AuthenticateAsync(new AuthenticationRequest
            {
                ClientId = GetVariable("AUTH0_CLIENT_ID"),
                ClientSecret = GetVariable("AUTH0_CLIENT_SECRET"),
                Connection = connection.Name,
                GrantType = "password",
                Scope = "openid offline_access",
                Username = user.Email,
                Password = "password"
            });

            // Assert
            authenticationResponse.Should().NotBeNull();
            authenticationResponse.TokenType.Should().NotBeNull();
            authenticationResponse.AccessToken.Should().NotBeNull();
            authenticationResponse.IdToken.Should().NotBeNull();
            authenticationResponse.RefreshToken.Should().NotBeNull(); // Requested offline access, so we should get a refresh token
        }

        [Test]
        public async Task Can_authenticate_user_with_plus_in_username()
        {
            // Arrange
            var authenticationApiClient = new AuthenticationApiClient(new Uri(GetVariable("AUTH0_AUTHENTICATION_API_URL")));

            // Act
            var authenticationResponse = await authenticationApiClient.AuthenticateAsync(new AuthenticationRequest
            {
                ClientId = GetVariable("AUTH0_CLIENT_ID"),
                ClientSecret = GetVariable("AUTH0_CLIENT_SECRET"),
                Connection = connection.Name,
                GrantType = "password",
                Scope = "openid",
                Username = plusUser.Email,
                Password = "password"
            });

            // Assert
            authenticationResponse.Should().NotBeNull();
            authenticationResponse.TokenType.Should().NotBeNull();
            authenticationResponse.AccessToken.Should().NotBeNull();
            authenticationResponse.IdToken.Should().NotBeNull();
        }

        [Test]
        public async Task Returns_username_and_password_login_form()
        {
            // Arrange
            var authenticationApiClient = new AuthenticationApiClient(new Uri(GetVariable("AUTH0_AUTHENTICATION_API_URL")));

            // Act
            var authenticationResponse = await authenticationApiClient.UsernamePasswordLoginAsync(new UsernamePasswordLoginRequest
            {
                ClientId = GetVariable("AUTH0_CLIENT_ID"),
                Connection = connection.Name,
                Scope = "openid",
                Username = user.Email,
                Password = "password",
                RedirectUri = "http://www.blah.com/test"
            });

            // Assert
            authenticationResponse.Should().NotBeNull();
            authenticationResponse.HtmlForm.Should().NotBeNull();


            // Load the form, and submit it
            var configuration = Configuration.Default.WithDefaultLoader().WithCookies();
            var context = BrowsingContext.New(configuration);
            await context.OpenAsync(request =>
            {
                request.Content(authenticationResponse.HtmlForm);
            });

            await context.Active.QuerySelector<IHtmlFormElement>("form").SubmitAsync();

            // Extract the URL and query from the postback
            var uri = new Uri(context.Active.Url);
            //var code = HttpUtility.ParseQueryString(uri.Query)["code"];

            // Assert that callback is made and code is passed back
            //code.Should().NotBeNull();
        }
    }
}