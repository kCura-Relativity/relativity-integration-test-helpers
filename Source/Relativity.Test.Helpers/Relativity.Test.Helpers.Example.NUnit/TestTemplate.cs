﻿using NUnit.Framework;
using Relativity.API;
using Relativity.Test.Helpers.SharedTestHelpers;
using System;
using System.IO;
using System.Reflection;
using IServicesMgr = Relativity.API.IServicesMgr;

namespace Relativity.Test.Helpers.Example.NUnit
{

	/// <summary>
	/// 
	/// Relativity Integration Test Helpers to assist you with writing good Integration Tests for your application. You can use this framework to test event handlers, agents and any workflow that combines agents and frameworks.
	/// 
	/// Before you get Started, fill out details for the following the app.config file
	/// "WorkspaceID", "RSAPIServerAddress", "RESTServerAddress",	"AdminUsername","AdminPassword", "SQLServerAddress" ,"SQLUsername","SQLPassword" "TestWorkspaceName"
	/// 
	/// </summary>

	[TestFixture, global::NUnit.Framework.Description("Fixture description here")]
	public class TestTemplate
	{

		#region Variables
		private int _workspaceId;
		private Int32 _rootFolderArtifactID;
		private string _workspaceName = $"IntTest_{Guid.NewGuid()}";
		private const ExecutionIdentity EXECUTION_IDENTITY = ExecutionIdentity.CurrentUser;
		private IDBContext dbContext;
		private IServicesMgr _servicesMgr;
		private IDBContext _eddsDbContext;
		private Int32 _numberOfDocuments = 5;
		private string _foldername = "Test Folder";
		private string _groupName = "Test Group";
		private int _userArtifactId;
		private int _groupArtifactId;
		private int _fixedLengthArtId;
		private int _longtextartid;
		private int _yesnoartid;
		private int _wholeNumberArtId;

		#endregion


		#region TestfixtureSetup

		[OneTimeSetUp]
		public void Execute_TestFixtureSetup()
		{
			//Setup for testing
			var helper = new TestHelper(ConfigurationHelper.ADMIN_USERNAME, ConfigurationHelper.DEFAULT_PASSWORD);
			_servicesMgr = helper.GetServicesManager();
			_eddsDbContext = helper.GetDBContext(-1);

			//Create new user 
			_userArtifactId = Relativity.Test.Helpers.UserHelpers.UserHelper.Create(helper.GetServicesManager());

			//Create new group
			Relativity.Test.Helpers.GroupHelpers.GroupHelper.CreateGroup(_servicesMgr, _groupName);


			//Create workspace
			_workspaceId = Helpers.WorkspaceHelpers.WorkspaceHelpers.CreateAsync(helper.GetServicesManager(), _workspaceName, SharedTestHelpers.ConfigurationHelper.TEST_WORKSPACE_TEMPLATE_NAME).Result;
			dbContext = helper.GetDBContext(_workspaceId);

			var executableLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var nativeFilePath = "";
			var nativeName = @"\\\\FakeFilePath\Natives\SampleTextFile.txt";
			if (executableLocation != null)
			{
				nativeFilePath = Path.Combine(executableLocation, nativeName);
			}
			//Create Documents with a given folder name
			Relativity.Test.Helpers.ImportAPIHelper.ImportAPIHelper.CreateDocumentswithFolderName(_workspaceId, _numberOfDocuments, _foldername, nativeFilePath);

			//Create Documents with a given folder artifact id
			var folderName = Relativity.Test.Helpers.ArtifactHelpers.FoldersHelper.GetFolderName(_rootFolderArtifactID, dbContext);
			Relativity.Test.Helpers.ImportAPIHelper.ImportAPIHelper.CreateDocumentswithFolderName(_workspaceId, _numberOfDocuments, folderName, nativeFilePath);

			//Create Fixed Length field
			_fixedLengthArtId = Relativity.Test.Helpers.ArtifactHelpers.FieldHelper.CreateFieldFixedLengthText(_servicesMgr, _workspaceId);

			//Create Long Text Field
			_longtextartid = Relativity.Test.Helpers.ArtifactHelpers.FieldHelper.CreateFieldLongText(_servicesMgr, _workspaceId);

			//Create Whole number field
			_wholeNumberArtId = Relativity.Test.Helpers.ArtifactHelpers.FieldHelper.CreateFieldWholeNumber(_servicesMgr, _workspaceId);

			_workspaceId = 1017834;
			var DtSearchAppArtifactId = 1038135;

			var guid = helper.GetGuid(_workspaceId, DtSearchAppArtifactId);
		}

		#endregion

		#region TestfixtureTeardown


		[OneTimeTearDown]
		public void Execute_TestFixtureTeardown()
		{
			//Delete Workspace
			WorkspaceHelpers.WorkspaceHelpers.Delete(_servicesMgr, _workspaceId);

			//Delete User
			UserHelpers.UserHelper.Delete(_servicesMgr, _userArtifactId);

			//Delete Group
			GroupHelpers.GroupHelper.DeleteGroup(_servicesMgr, _groupArtifactId);
		}


		#endregion

		#region region Golden Flow

		[Test, global::NUnit.Framework.Description("Test description here")]
		public void Integration_Test_Golden_Flow_Valid()
		{
			//Arrange

			//Act

			//Assert

		}

		#endregion

	}

}