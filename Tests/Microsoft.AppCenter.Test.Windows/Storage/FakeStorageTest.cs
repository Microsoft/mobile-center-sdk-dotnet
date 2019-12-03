// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AppCenter.Ingestion.Models;
using Microsoft.AppCenter.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AppCenter.Windows.Shared.Storage;
using SQLitePCL;
using Moq;

namespace Microsoft.AppCenter.Test
{
    using PredType = Expression<Func<Microsoft.AppCenter.Storage.Storage.LogEntry, bool>>;

    [TestClass]
    public class FakeStorageTest
    {

        private const string TableName = "LogEntry";
        private const string StorageTestChannelName = "storageTestChannelName";
        
        /// <summary>
        /// Verify that shutdown fails when tasks exceed time limit
        /// </summary>
        [TestMethod]
        public void ShutdownTimeout()
        {
            var mockConnection = new Mock<IStorageAdapter>();
            mockConnection.Setup(
                    c => c.InsertAsync(TableName, It.IsAny<List<List<ColumnValueMap>>>()))
                    .Callback(() => Task.Delay(TimeSpan.FromDays(1)).Wait())
                .Returns(TaskExtension.GetCompletedTask(1));
            var storage = new Microsoft.AppCenter.Storage.Storage(mockConnection.Object);

            // Ignore warnings because we just want to "fire and forget"
#pragma warning disable 4014
            storage.PutLog(StorageTestChannelName, new TestLog());
            storage.PutLog(StorageTestChannelName, new TestLog());
#pragma warning restore 4014

            var result = storage.ShutdownAsync(TimeSpan.FromTicks(1)).RunNotAsync();
            Assert.IsFalse(result);
        }

        /// <summary>
        /// Verify that shutdown is successful when tasks complete in time
        /// </summary>
        [TestMethod]
        public void ShutdownSucceed()
        {
            var mockConnection = new Mock<IStorageAdapter>();
            mockConnection.Setup(
                    c => c.InsertAsync(TableName, It.IsAny<List<List<ColumnValueMap>>>()))
                .Callback(() => Task.Delay(TimeSpan.FromSeconds(2)).Wait())
                .Returns(TaskExtension.GetCompletedTask(1));
            var storage = new Microsoft.AppCenter.Storage.Storage(mockConnection.Object);
            
            // Ignore warnings because we just want to "fire and forget"
#pragma warning disable 4014
            storage.PutLog(StorageTestChannelName, new TestLog());
            storage.PutLog(StorageTestChannelName, new TestLog());
#pragma warning restore 4014

            var result = storage.ShutdownAsync(TimeSpan.FromSeconds(100)).RunNotAsync();
            Assert.IsTrue(result);
        }

        /// <summary>
        /// Verify that new tasks are not started after shutdown
        /// </summary>
        [TestMethod]
        public void ShutdownPreventsNewTasks()
        {
            var mockConnection = new Mock<IStorageAdapter>();
            var storage = new Microsoft.AppCenter.Storage.Storage(mockConnection.Object);
            var result = storage.ShutdownAsync(TimeSpan.FromSeconds(10)).RunNotAsync();
            Assert.IsTrue(result);
            Assert.ThrowsException<StorageException>(
                () => storage.GetLogsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<List<Log>>()).RunNotAsync());
        }

        /// <summary>
        /// Verify that if an error occurs while executing a query (from GetLogs), a StorageException gets thrown.
        /// </summary>
        [TestMethod]
        public void GetLogsQueryError()
        {
            var mockAdapter = new Mock<IStorageAdapter>();
            mockAdapter.Setup(
                    a => a.GetAsync(TableName, It.IsAny<String>(), It.IsAny<int>()))
                    .Returns(TaskExtension.GetFaultedTask<List<List<object>>>(new StorageException()));
            var fakeStorage = new Microsoft.AppCenter.Storage.Storage(mockAdapter.Object);
            var logs = new List<Log>();
            Assert.ThrowsException<StorageException>(() =>
                fakeStorage.GetLogsAsync(StorageTestChannelName, 1, logs).RunNotAsync());
        }

        /// <summary>
        /// Verify that storage throws StorageException if something went wrong
        /// </summary>
        [TestMethod]
        public void StorageThrowsStorageException()
        {
            var mockAdapter = new Mock<IStorageAdapter>();
            mockAdapter.Setup(
                    a => a.GetAsync(TableName, It.IsAny<string>(), It.IsAny<int>()))
                .Returns(TaskExtension.GetFaultedTask<List<List<object>>>(new StorageException()));
            mockAdapter.Setup(c => c.InsertAsync(TableName, It.IsAny<List<List<ColumnValueMap>>>()))
                .Returns(TaskExtension.GetFaultedTask<int>(new StorageException()));
            mockAdapter.Setup(c => c.DeleteAsync(TableName, It.IsAny<string>()))
                .Returns(TaskExtension.GetFaultedTask<int>(new StorageException()));
            mockAdapter.Setup(c => c.CountAsync(TableName, It.IsAny<string>()))
                .Returns(TaskExtension.GetFaultedTask<int>(new StorageException()));
            var fakeStorage = new Microsoft.AppCenter.Storage.Storage(mockAdapter.Object);
            Assert.ThrowsException<StorageException>(() => fakeStorage.PutLog(StorageTestChannelName, new TestLog()).RunNotAsync());
            Assert.ThrowsException<StorageException>(() => fakeStorage.DeleteLogs(StorageTestChannelName, string.Empty).RunNotAsync());
            Assert.ThrowsException<StorageException>(() => fakeStorage.CountLogsAsync(StorageTestChannelName).RunNotAsync());
            Assert.ThrowsException<StorageException>(() => fakeStorage.GetLogsAsync(StorageTestChannelName, 1, new List<Log>()).RunNotAsync());
        }
    }
}
