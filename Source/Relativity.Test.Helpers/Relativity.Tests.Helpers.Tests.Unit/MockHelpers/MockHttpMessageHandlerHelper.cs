﻿using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Relativity.Tests.Helpers.Tests.Unit.MockHelpers
{
	public static class MockHttpMessageHandlerHelper
	{
		/// <summary>
		/// Simple GET call mocked here for bearer token
		/// </summary>
		/// <param name="jsonPayloadWithBearerToken"></param>
		/// <returns></returns>
		public static Mock<HttpMessageHandler> GetMockHttpMessageHandlerForBearerToken(string jsonPayloadWithBearerToken)
		{
			Mock<HttpMessageHandler> mockHttpMessageHandler = new Mock<HttpMessageHandler>();

			mockHttpMessageHandler.Protected()
				.Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
				.ReturnsAsync(new HttpResponseMessage
				{
					StatusCode = HttpStatusCode.OK,
					Content = new StringContent(jsonPayloadWithBearerToken)
				});

			return mockHttpMessageHandler;
		}

		/// <summary>
		/// Simple POST call mocked here to get Relativity Version
		/// </summary>
		/// <param name="expectedRelativityVersion"></param>
		/// <returns></returns>
		public static Mock<HttpMessageHandler> GetMockHttpMessageHandlerForRelativityVersion(string expectedRelativityVersion)
		{
			Mock<HttpMessageHandler> mockHttpMessageHandler = new Mock<HttpMessageHandler>();

			mockHttpMessageHandler.Protected()
				.Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Post && m.RequestUri.OriginalString.Contains("GetRelativityVersionAsync")), ItExpr.IsAny<CancellationToken>())
				.ReturnsAsync(new HttpResponseMessage { Content = new StringContent(expectedRelativityVersion) });

			return mockHttpMessageHandler;
		}
	}
}
