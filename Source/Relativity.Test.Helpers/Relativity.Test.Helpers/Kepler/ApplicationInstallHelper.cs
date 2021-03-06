﻿using Relativity.Kepler.Transport;
using Relativity.Services.Interfaces.LibraryApplication;
using Relativity.Services.Interfaces.LibraryApplication.Models;
using Relativity.Test.Helpers.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Relativity.Test.Helpers.Kepler
{
	public class ApplicationInstallHelper : IApplicationInstallHelper, IDisposable
	{
		private const int AdminWorkspaceId = -1;
		private const string MinimumKeplerCompatibilityVersion = "10.3.170.1";
		private const int LibraryApplicationTypeId = 34;
		private const int WorkspaceApplicationTypeId = 1000011;
		private const int TimeInMinutesToWaitForInstall = 5;

		private readonly IApplicationInstallManager _applicationInstallManager;
		private readonly ILibraryApplicationManager _libraryApplicationManager;
		private readonly string _protocol;
		private readonly string _serverAddress;
		private readonly string _username;
		private readonly string _password;
		private readonly HttpMessageHandler _httpMessageHandler;
		private string _relativityVersion;

		/// <summary>
		/// The ApplicationInstallHelper takes in the 3 proxies as required and will use them depending on if your Relativity Instance is 10.3.170.1 or greater.
		/// </summary>
		/// <param name="applicationInstallManager"></param>
		/// <param name="libraryApplicationManager"></param>
		/// <param name="protocol"></param>
		/// <param name="serverAddress"></param>
		/// <param name="username"></param>
		/// <param name="password"></param>
		/// <param name="httpMessageHandler"></param>

		public ApplicationInstallHelper(IApplicationInstallManager applicationInstallManager, ILibraryApplicationManager libraryApplicationManager, string protocol, string serverAddress, string username, string password, HttpMessageHandler httpMessageHandler = null)
		{
			_applicationInstallManager = applicationInstallManager ?? throw new ArgumentNullException($"Parameter ({nameof(applicationInstallManager)}) cannot be null");
			_libraryApplicationManager = libraryApplicationManager ?? throw new ArgumentNullException($"Parameter ({nameof(libraryApplicationManager)}) cannot be null");
			_protocol = protocol ?? throw new ArgumentNullException($"Parameter ({nameof(_protocol)}) cannot be null");
			_serverAddress = serverAddress ?? throw new ArgumentNullException($"Parameter ({nameof(serverAddress)}) cannot be null");
			_username = username ?? throw new ArgumentNullException($"Parameter ({nameof(username)}) cannot be null");
			_password = password ?? throw new ArgumentNullException($"Parameter ({nameof(password)}) cannot be null");

			if (httpMessageHandler == null)
			{
				_httpMessageHandler = new HttpClientHandler()
				{
					AllowAutoRedirect = false,
					UseDefaultCredentials = true
				};
			}
			else
			{
				_httpMessageHandler = httpMessageHandler;
			}

			_relativityVersion = "";
		}

		public void Dispose()
		{
			_applicationInstallManager.Dispose();
			_libraryApplicationManager.Dispose();
		}

		/// <summary>
		/// Installs the application into a workspace.  Will install the application into the Library if it does not exist there already
		/// </summary>
		/// <param name="applicationName"></param>
		/// <param name="fileStream"></param>
		/// <param name="workspaceId"></param>
		/// <param name="unlockApps"></param>
		/// <returns></returns>
		public async Task<int> InstallApplicationAsync(string applicationName, FileStream fileStream, int workspaceId, bool unlockApps)
		{
			try
			{
				List<int> workspaces = new List<int> { workspaceId };
				int workspaceApplicationInstallId;
				int libraryApplicationInstallId;
				InstallStatusCode status;

				InstallApplicationRequest request = new InstallApplicationRequest
				{
					WorkspaceIDs = workspaces,
					UnlockApplications = unlockApps
				};


				if (!await DoesLibraryApplicationExistAsync(applicationName))
				{
					libraryApplicationInstallId = await CreateLibraryApplicationAsync(fileStream);

					// The following function will poll for the installation status until the installation reaches a terminal state.
					status = PollForTerminalStatusAsync(async () => await _libraryApplicationManager.GetLibraryInstallStatusAsync(AdminWorkspaceId, libraryApplicationInstallId)).ConfigureAwait(false).GetAwaiter().GetResult();
					Console.WriteLine($@"Library Installation has terminated with the following status: {status}.");
				}
				else
				{
					libraryApplicationInstallId = await GetLibraryApplicationIdAsync(applicationName);
				}

				if (workspaceId != -1)
				{
					InstallApplicationResponse response = await _applicationInstallManager.InstallApplicationAsync(AdminWorkspaceId, libraryApplicationInstallId, request);

					if (response.Results.Count == workspaces.Count)
					{
						string info = string.Format($"Queuing {response.Results.Count} installation(s) for the Library Application with ArtifactID {libraryApplicationInstallId}.");
						Console.WriteLine(info);
					}
					else
					{
						string info = string.Format($"Queuing {response.Results.Count} installation(s) for the Library Application with ArtifactID {libraryApplicationInstallId} " +
																				$"since one or more workspaces already have the same version of this application installed.");
						Console.WriteLine(info);
					}

					workspaceApplicationInstallId = response.Results.First().ApplicationInstallID;

					status = PollForTerminalStatusAsync(async () => await _applicationInstallManager.GetStatusAsync(AdminWorkspaceId, libraryApplicationInstallId, workspaceApplicationInstallId)).ConfigureAwait(false).GetAwaiter().GetResult();
					Console.WriteLine($@"Workspace Installation has terminated with the following status: {status}.");
				}
				else
				{
					workspaceApplicationInstallId = libraryApplicationInstallId;
				}

				return workspaceApplicationInstallId;
			}
			catch (Exception ex)
			{
				string exception = $"An error occurred: {ex.Message}";
				Console.WriteLine(exception);
				throw new TestHelpersApplicationInstallException(exception, ex);
			}
		}

		/// <summary>
		/// Deletes the application from the Library if it exists.
		/// </summary>
		/// <param name="applicationName"></param>
		/// <returns></returns>
		public async Task DeleteApplicationFromLibraryIfItExistsAsync(string applicationName)
		{
			try
			{
				if (await DoesLibraryApplicationExistAsync(applicationName))
				{
					int applicationId = await GetLibraryApplicationIdAsync(applicationName);

					await _libraryApplicationManager.DeleteAsync(AdminWorkspaceId, applicationId);
					string info = string.Format($"Library Application with ArtifactID ({applicationId}) and applicationName ({applicationName}) is being deleted.");
					Console.WriteLine(info);
				}
				else
				{
					string info = string.Format($"Library Application with applicationName ({applicationName}) does not exist");
					Console.WriteLine(info);
				}
			}
			catch (Exception ex)
			{
				string exception = $"An error occurred: {ex.Message}";
				Console.WriteLine(exception);
				throw new TestHelpersApplicationInstallException(exception, ex);
			}
		}

		/// <summary>
		/// Checks if the application exists in the Library
		/// </summary>
		/// <param name="applicationName"></param>
		/// <returns></returns>
		public async Task<bool> DoesLibraryApplicationExistAsync(string applicationName)
		{
			try
			{
				bool result;

				List<LibraryApplicationResponse> allApps = await _libraryApplicationManager.ReadAllAsync(AdminWorkspaceId);
				result = allApps.Exists(x => x.Name.Equals(applicationName));


				return result;
			}
			catch (Exception ex)
			{
				string exception = $"An error occurred in ({nameof(DoesLibraryApplicationExistAsync)}) | applicationName: ({applicationName}) : {ex.Message}";
				Console.WriteLine(exception);
				throw new TestHelpersApplicationInstallException(exception, ex);
			}
		}

		/// <summary>
		/// Checks if the application exists in the given workspace
		/// </summary>
		/// <param name="applicationName"></param>
		/// <param name="workspaceId"></param>
		/// <param name="workspaceApplicationInstallId"></param>
		/// <returns></returns>
		public async Task<bool> DoesWorkspaceApplicationExistAsync(string applicationName, int workspaceId, int workspaceApplicationInstallId)
		{
			try
			{
				bool result;

				if (workspaceId != AdminWorkspaceId)
				{
					List<LibraryApplicationResponse> allApps = await _libraryApplicationManager.ReadAllAsync(AdminWorkspaceId);

					if (allApps.Exists(x => x.Name.Equals(applicationName)))
					{
						LibraryApplicationResponse app = allApps.Find(x => x.Name.Equals(applicationName));

						GetInstallStatusResponse appStatus = await _applicationInstallManager.GetStatusAsync(AdminWorkspaceId, app.ArtifactID, workspaceApplicationInstallId);
						result = appStatus.InstallStatus.Code == InstallStatusCode.Completed;
					}
					else
					{
						result = false;
					}

				}
				else
				{
					result = await DoesLibraryApplicationExistAsync(applicationName);
				}

				return result;
			}
			catch (Exception ex)
			{
				string exception = $"An error occurred in ({nameof(DoesWorkspaceApplicationExistAsync)}) | applicationName: ({applicationName}) | workspaceId: ({workspaceId}) | workspaceApplicationInstallId: ({workspaceApplicationInstallId}) : {ex.Message}";
				Console.WriteLine(exception);
				throw new TestHelpersApplicationInstallException(exception, ex);
			}
		}


		#region private methods

		/// <summary>
		/// New API Kepler calls to install the application into the library.  Will error if it already exists
		/// </summary>
		/// <param name="fileStream"></param>
		/// <returns></returns>
		private async Task<int> CreateLibraryApplicationAsync(FileStream fileStream)
		{
			try
			{
				CreateLibraryApplicationResponse response = await _libraryApplicationManager.CreateAsync(AdminWorkspaceId, new KeplerStream(fileStream));
				string info = string.Format($"The file located at {fileStream.Name} is uploading to the application library.");
				Console.WriteLine(info);

				return response.ApplicationIdentifier.ArtifactID;
			}
			catch (Exception ex)
			{
				string exception = $"An error occurred: {ex.Message}";
				Console.WriteLine(exception);
				throw new TestHelpersApplicationInstallException(exception, ex);
			}
		}

		/// <summary>
		/// Gets the ArtifactId for the Library Application, but will error if it does not exist
		/// </summary>
		/// <param name="applicationName"></param>
		/// <returns></returns>
		private async Task<int> GetLibraryApplicationIdAsync(string applicationName)
		{
			try
			{
				List<LibraryApplicationResponse> allApps;
				int result;

				allApps = await _libraryApplicationManager.ReadAllAsync(AdminWorkspaceId);
				bool exists = allApps.Exists(x => x.Name.Equals(applicationName));

				if (exists)
				{
					result = allApps.Find(x => x.Name.Equals(applicationName)).ArtifactID;
				}
				else
				{
					throw new TestHelpersApplicationInstallException("Library Application does not exist");
				}

				return result;
			}
			catch (Exception ex)
			{
				string exception = $"An error occurred in ({nameof(GetLibraryApplicationIdAsync)}) | applicationName: ({applicationName}) : {ex.Message}";
				Console.WriteLine(exception);
				throw new TestHelpersApplicationInstallException(exception, ex);
			}
		}

		/// <summary>
		/// Checks to see if the Relativity Instance is >= the version that has the new Install APIs
		/// </summary>
		/// <returns></returns>
		private async Task<bool> IsVersionKeplerCompatibleAsync()
		{
			try
			{
				string currentInstanceRelativity = await GetInstanceRelativityVersionAsync();

				Version minVersion = new Version(MinimumKeplerCompatibilityVersion);
				Version currentInstanceVersion = new Version(currentInstanceRelativity);

				int result = currentInstanceVersion.CompareTo(minVersion);

				return result >= 0;
			}
			catch (Exception ex)
			{
				string exception = $"An error occurred in ({nameof(IsVersionKeplerCompatibleAsync)}) : {ex.Message}";
				Console.WriteLine(exception);
				throw new TestHelpersApplicationInstallException(exception, ex);
			}
		}

		/// <summary>
		/// The REST call to get the Relativity Version.
		/// </summary>
		/// <returns></returns>
		private async Task<string> GetInstanceRelativityVersionAsync()
		{
			try
			{
				if (string.IsNullOrEmpty(_relativityVersion))
				{
					HttpClient httpClient = new HttpClient(_httpMessageHandler);

					httpClient.BaseAddress = new Uri($"{_protocol}://{_serverAddress}/Relativity");
					string encoded = System.Convert.ToBase64String(Encoding.ASCII.GetBytes(_username + ":" + _password));
					httpClient.DefaultRequestHeaders.Add("X-CSRF-Header", string.Empty);
					httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {encoded}");
					StringContent content = new StringContent("");
					content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

					string url = "/relativity.rest/api/Relativity.Services.InstanceDetails.IInstanceDetailsModule/InstanceDetailsService/GetRelativityVersionAsync";
					HttpResponseMessage httpResponse = await httpClient.PostAsync(url, content);
					string instanceRelativityVersion = await httpResponse.Content.ReadAsStringAsync();
					instanceRelativityVersion = instanceRelativityVersion.Replace("\"", "");
					_relativityVersion = instanceRelativityVersion;
					return instanceRelativityVersion;
				}
				else
				{
					return await Task.FromResult(_relativityVersion);
				}
			}
			catch (Exception ex)
			{
				string exception = $"An error occurred in ({nameof(GetInstanceRelativityVersionAsync)}) : {ex.Message}";
				Console.WriteLine(exception);
				throw new TestHelpersApplicationInstallException(exception, ex);
			}
		}

		/// <summary>
		/// This is used while installing in the new API to determine when the application is done installing
		/// </summary>
		/// <param name="func"></param>
		/// <returns></returns>
		private async Task<InstallStatusCode> PollForTerminalStatusAsync(Func<Task<GetInstallStatusResponse>> func)
		{
			try
			{
				GetInstallStatusResponse result;
				Stopwatch watch = new Stopwatch();
				int refreshInterval = 5;
				bool continuePolling = true;
				watch.Start();

				do
				{
					if (watch.Elapsed.TotalMinutes > TimeInMinutesToWaitForInstall)
					{
						throw new System.Exception($"The terminal status for this application installation could not be obtained. " +
																			 $"Total polling time exceeded timeout value of {TimeInMinutesToWaitForInstall} minutes.");
					}

					result = await func.Invoke();

					switch (result.InstallStatus.Code)
					{
						case InstallStatusCode.Pending:
						case InstallStatusCode.InProgress:
							Console.WriteLine(@"Application installation is pending or in progress.");
							break;

						default:
							continuePolling = false;
							break;
					}

					if (continuePolling)
					{
						await Task.Delay(TimeSpan.FromSeconds(refreshInterval));
					}

				} while (continuePolling);

				watch.Stop();
				return result.InstallStatus.Code;
			}
			catch (Exception ex)
			{
				string exception = $"An error occurred in ({nameof(PollForTerminalStatusAsync)}) : {ex.Message}";
				Console.WriteLine(exception);
				throw new TestHelpersApplicationInstallException(exception, ex);
			}
		}

		#endregion
	}
}
