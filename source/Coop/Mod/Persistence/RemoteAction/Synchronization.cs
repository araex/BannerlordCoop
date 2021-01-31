﻿using System;
using System.Collections.Generic;
using System.Linq;
using CoopFramework;
using JetBrains.Annotations;
using NLog;
using RemoteAction;
using Sync;

namespace Coop.Mod.Persistence.RemoteAction
{
    public class Synchronization : ISynchronization
    {
        public Synchronization([NotNull] IClientAccess access)
        {
            m_ClientAccess = access;
        }

        public void Broadcast(MethodId id, object instance, object[] args)
        {
            var access = Sync.Registry.IdToMethod[id];
            var bDebounce = access.Flags.HasFlag(EMethodPatchFlag.DebounceCalls);
            var call = new MethodCall(
                id,
                ArgumentFactory.Create(
                    m_ClientAccess.GetStore(),
                    instance,
                    false),
                ProduceArguments(access.Flags, args));

            if (bDebounce && PendingRequests.Instance.IsPending(call))
            {
                Logger.Debug("Debounced RPC {}", call);
            }
            else
            {
                PendingRequests.Instance.Add(call);
                m_ClientAccess.GetRoom()
                    ?.RaiseEvent<EventMethodCall>(
                        evt =>
                        {
                            evt.Call = call;
                            BroadcastHistory.Push(evt.Call, m_ClientAccess.GetRoom().Tick);
                        });
            }
        }

        public void RegisterSyncedField(ValueAccess value, IEnumerable<MethodAccess> triggers, Func<bool> condition)
        {
            throw new NotImplementedException();
        }


        #region Debug

        public CallStatistics BroadcastHistory { get; } = new CallStatistics();

        #endregion

        #region Private

        private List<Argument> ProduceArguments(EMethodPatchFlag flags, object[] args)
        {
            var bTransferByValue = flags.HasFlag(EMethodPatchFlag.TransferArgumentsByValue);
            return args.Select(
                    obj => ArgumentFactory.Create(
                        m_ClientAccess.GetStore(),
                        obj,
                        bTransferByValue))
                .ToList();
        }

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        [NotNull] private readonly IClientAccess m_ClientAccess;

        #endregion
    }
}