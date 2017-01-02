﻿using System;
using System.Threading.Tasks;
using Auth0.AuthenticationApi.Models;
using Auth0.Tests.Shared;
using FluentAssertions;
using NUnit.Framework;
using Auth0.ManagementApi;
using Auth0.Core;
using Auth0.ManagementApi.Models;

namespace Auth0.AuthenticationApi.IntegrationTests
{
    [TestFixture]
    public class AccessTokenTests : TestBase
    {
        private string socialAccessToken = "your google access token";
        private ManagementApiClient managementApiClient;
        private Connection connection;
        private User newUser;

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
                }
            };
            string token = GenerateToken(scopes);

            managementApiClient = new ManagementApiClient(token, new Uri(GetVariable("AUTH0_MANAGEMENT_API_URL")));

            // We will need a connection to add the users to...
            connection = await managementApiClient.Connections.CreateAsync(new ConnectionCreateRequest
            {
                Name = Guid.NewGuid().ToString("N"),
                Strategy = "auth0",
                EnabledClients = new[] { GetVariable("AUTH0_CLIENT_ID") }
            });

            // Add a new user
            var newUserRequest = new UserCreateRequest
            {
                Connection = connection.Name,
                Email = $"{Guid.NewGuid():N}@nonexistingdomain.aaa",
                EmailVerified = true,
                Password = "password"
            };

            newUser = await managementApiClient.Users.CreateAsync(newUserRequest);

        }

        [TearDown]
        public async Task TearDown()
        {
            if (connection != null)
                await managementApiClient.Connections.DeleteAsync(connection.Id);
        }

        [Test, Explicit]
        public async Task Can_get_delegation_token()
        {
            var authenticationApiClient = new AuthenticationApiClient(new Uri(GetVariable("AUTH0_AUTHENTICATION_API_URL")));
            
            // First get the access token
            var token = await authenticationApiClient.AuthenticateAsync(new AuthenticationRequest
            {
                ClientId = GetVariable("AUTH0_CLIENT_ID"),
                ClientSecret = GetVariable("AUTH0_CLIENT_SECRET"),
                Connection = connection.Name,
                Username = newUser.Email,
                Password = "password",
                Scope = "openid"
            });

            // Then request the delegation token
            var delegationToken = await authenticationApiClient.GetDelegationTokenAsync(new IdTokenDelegationRequest(
                GetVariable("AUTH0_CLIENT_ID"),
                GetVariable("AUTH0_CLIENT_ID"),
                token.IdToken)
            {
                Scope = "openid",
                GrantType = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ApiType = "app"
            });

            delegationToken.Should().NotBeNull();
            delegationToken.IdToken.Should().NotBeNull();
        }

        [Test, Explicit]
        [Ignore("updated_at is not a long value as expected. this will be fixed soon.")]
        public async Task Can_obtain_user_info()
        {
            var authenticationApiClient = new AuthenticationApiClient(new Uri(GetVariable("AUTH0_AUTHENTICATION_API_URL")));

            // First get the access token
            var token = await authenticationApiClient.AuthenticateAsync(new AuthenticationRequest
            {
                ClientId = GetVariable("AUTH0_CLIENT_ID"),
                ClientSecret = GetVariable("AUTH0_CLIENT_SECRET"),
                Connection = connection.Name,
                Username = newUser.Email,
                Password = "password",
                Scope = "openid profile"
            });

            // Get the user info
            var user = await authenticationApiClient.GetUserInfoAsync(token.AccessToken);
            user.Should().NotBeNull();
            user.Email.Should().NotBeNull();
        }

        [Test, Explicit]
        public async Task Can_exchange_authorization_code_for_access_token()
        {
            var authenticationApiClient = new AuthenticationApiClient(new Uri(GetVariable("AUTH0_AUTHENTICATION_API_URL")));

            // Exchange the authorization code
            var token = await authenticationApiClient.ExchangeCodeForAccessTokenAsync(new ExchangeCodeRequest
            {
                ClientId = GetVariable("AUTH0_CLIENT_ID"),
                ClientSecret = GetVariable("AUTH0_CLIENT_SECRET"),
                RedirectUri = "http://www.blah.com/test",
                AuthorizationCode = "AaBhdAOl4OKvjX2I"
            });

            // Assert
            token.Should().NotBeNull();
        }
    }
}