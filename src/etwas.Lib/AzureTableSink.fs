module AzureTableSink

open Microsoft.Diagnostics.Tracing
open System.Diagnostics.Tracing
open Microsoft.Practices.EnterpriseLibrary.SemanticLogging
open Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Schema
open System
open System.Collections.ObjectModel

let translateToInProc(e: TraceEvent): EventEntry =
    let payloadLength = Array.length e.PayloadNames
    let payload = new ReadOnlyCollection<_>(Array.init payloadLength e.PayloadValue)
    let schema = new EventSchema(int(e.ID),
                                 e.ProviderGuid,
                                 e.ProviderName,
                                 enum<EventLevel>(int(e.Level)),
                                 enum<EventTask>(int(e.Task)),
                                 e.TaskName,
                                 enum<EventOpcode>(int(e.Opcode)),
                                 e.OpcodeName,
                                 Unchecked.defaultof<EventKeywords>, (*e.Keywords*)// TODO: Fix Keyword translation
                                 "keywordsDescription",                            // TODO: Fix Keyword translation
                                 e.Version,
                                 e.PayloadNames)
    new EventEntry(Guid.Empty, int(e.ID), e.FormattedMessage, payload, new DateTimeOffset(e.TimeStamp), schema)