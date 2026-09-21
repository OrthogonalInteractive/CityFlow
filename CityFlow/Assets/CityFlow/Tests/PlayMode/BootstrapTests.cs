#nullable enable

using System.Collections;
using CityFlow.Composition;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityFlow.Tests.PlayMode
{
    public sealed class BootstrapTests
    {
        [UnityTest]
        public IEnumerator BootstrapSceneBuildsItsContainer()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);

            CityFlowLifetimeScope scope = Object.FindAnyObjectByType<CityFlowLifetimeScope>();
            Assert.That(scope, Is.Not.Null);
            Assert.That(scope.Container, Is.Not.Null);
            Assert.That(Camera.main, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator UniTaskAndR3ShareTheUnityPlayerLoop()
        {
            return UniTask.ToCoroutine(async () =>
            {
                int updates = 0;
                using (Observable.EveryUpdate().Subscribe(_ => updates++))
                {
                    await UniTask.NextFrame();
                    await UniTask.NextFrame();
                    Assert.That(updates, Is.GreaterThan(0));
                }

                int updatesAfterDisposal = updates;
                await UniTask.NextFrame();
                Assert.That(updates, Is.EqualTo(updatesAfterDisposal));
            });
        }
    }
}
