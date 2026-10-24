using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Severnbli.LocalizationCodegen.Tests
{
    public class LocalizedTablesUniTaskTests
    {
        /// <summary>An operation the test completes by hand.</summary>
        sealed class ManualOperation : AsyncOperationBase<string>
        {
            protected override void Execute()
            {
            }

            public void Succeed(string result) => Complete(result, true, (string)null);
        }

        ResourceManager m_ResourceManager;

        [SetUp]
        public void SetUp()
        {
            m_ResourceManager = new ResourceManager();
        }

        [TearDown]
        public void TearDown()
        {
            m_ResourceManager.Dispose();
        }

        static void IgnoreWhenSettingsExist()
        {
            if (LocalizationSettings.HasSettings)
                Assert.Ignore("This test project has LocalizationSettings configured.");
        }

        [Test]
        public void GetStringAsync_ReturnsKey_WhenSettingsAreMissing()
        {
            IgnoreWhenSettingsExist();

            var task = LocalizedTablesUniTask.GetStringAsync("Table", "Key");
            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
            Assert.AreEqual("Key", task.GetAwaiter().GetResult());
        }

        [Test]
        public void GetStringAsyncWithArguments_ReturnsKey_WhenSettingsAreMissing()
        {
            IgnoreWhenSettingsExist();

            var named = new Dictionary<string, object> { ["count"] = 2 };
            var task = LocalizedTablesUniTask.GetStringAsync("Table", "Key", new object[] { 1 }, named);
            Assert.AreEqual("Key", task.GetAwaiter().GetResult());
        }

        [Test]
        public void GetAssetAsync_ReturnsNull_WhenSettingsAreMissing()
        {
            IgnoreWhenSettingsExist();

            var task = LocalizedTablesUniTask.GetAssetAsync<Sprite>("Table", "Key");
            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
            Assert.IsNull(task.GetAwaiter().GetResult());
        }

        [Test]
        public void GetLocalizedStringUniTask_ReturnsKey_WhenSettingsAreMissing()
        {
            IgnoreWhenSettingsExist();

            var task = new LocalizedString("Table", "Key").GetLocalizedStringUniTask();
            Assert.AreEqual("Key", task.GetAwaiter().GetResult());
        }

        [Test]
        public void AlreadyCancelledToken_ReturnsCancelledTask()
        {
            var token = new CancellationToken(true);

            Assert.AreEqual(UniTaskStatus.Canceled, LocalizedTablesUniTask.GetStringAsync("Table", "Key", token).Status);
            Assert.AreEqual(UniTaskStatus.Canceled, LocalizedTablesUniTask.GetAssetAsync<Sprite>("Table", "Key", token).Status);
        }

        [Test]
        public void ToUniTask_CompletesWhenTheOperationCompletes()
        {
            var operation = new ManualOperation();
            var handle = m_ResourceManager.StartOperation(operation, default);

            var task = LocalizedTablesUniTask.ToUniTask(handle, CancellationToken.None);
            Assert.AreEqual(UniTaskStatus.Pending, task.Status);

            operation.Succeed("Hello");
            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
            Assert.AreEqual("Hello", task.GetAwaiter().GetResult());

            handle.Release();
        }

        [Test]
        public void ToUniTask_CancelsWithoutWaitingForTheOperation()
        {
            var operation = new ManualOperation();
            var handle = m_ResourceManager.StartOperation(operation, default);

            using (var cancellation = new CancellationTokenSource())
            {
                var task = LocalizedTablesUniTask.ToUniTask(handle, cancellation.Token);
                cancellation.Cancel();
                Assert.AreEqual(UniTaskStatus.Canceled, task.Status);

                // A late completion must not throw or change the result.
                Assert.DoesNotThrow(() => operation.Succeed("Late"));
                Assert.AreEqual(UniTaskStatus.Canceled, task.Status);
            }

            handle.Release();
        }

        [Test]
        public void ToUniTask_ReturnsTheResultOfACompletedOperation()
        {
            var handle = m_ResourceManager.CreateCompletedOperation("Done", null);

            var task = LocalizedTablesUniTask.ToUniTask(handle, CancellationToken.None);
            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
            Assert.AreEqual("Done", task.GetAwaiter().GetResult());

            handle.Release();
        }

        [Test]
        public void ToUniTask_FaultsForAFailedOperation()
        {
            var handle = m_ResourceManager.CreateCompletedOperationWithException<string>(
                null, new InvalidOperationException("Table missing"));

            var task = LocalizedTablesUniTask.ToUniTask(handle, CancellationToken.None);
            Assert.AreEqual(UniTaskStatus.Faulted, task.Status);
            Assert.Throws(Is.InstanceOf<Exception>(), () => task.GetAwaiter().GetResult());

            handle.Release();
        }

        [Test]
        public void ToUniTask_FaultsForAnInvalidHandle()
        {
            var task = LocalizedTablesUniTask.ToUniTask(default(AsyncOperationHandle<string>), CancellationToken.None);
            Assert.AreEqual(UniTaskStatus.Faulted, task.Status);
            Assert.Throws<InvalidOperationException>(() => task.GetAwaiter().GetResult());
        }
    }
}
