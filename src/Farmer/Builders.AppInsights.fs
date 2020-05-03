﻿[<AutoOpen>]
module Farmer.Resources.AppInsights

open Farmer
open Arm.Insights

let tryCreateAppInsightsName aiName rootName =
    aiName
    |> Option.map(function
    | AutomaticPlaceholder ->
        AutomaticallyCreated(ResourceName(sprintf "%s-ai" rootName))
    | (External _ as resourceRef)
    | (AutomaticallyCreated _ as resourceRef) ->
        resourceRef)
let instrumentationKey (ResourceName accountName) =
    sprintf "reference('Microsoft.Insights/components/%s').InstrumentationKey" accountName
    |> ArmExpression

type AppInsightsConfig =
    { Name : ResourceName }
    /// Gets the ARM expression path to the instrumentation key of this App Insights instance.
    member this.InstrumentationKey = instrumentationKey this.Name
    interface IResourceBuilder with
        member this.BuildResources location = [
            NewResource { Name = this.Name
                          Location = location
                          LinkedWebsite = None }
        ]

type AppInsightsBuilder() =
    member __.Yield _ =
        { Name = ResourceName.Empty }
    [<CustomOperation "name">]
    /// Sets the name of the App Insights instance.
    member __.Name(state:AppInsightsConfig, name) = { state with Name = ResourceName name }

let appInsights = AppInsightsBuilder()