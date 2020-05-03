[<AutoOpen>]
module Farmer.Resources.ServiceBus

open Farmer
open Arm.ServiceBus

type MessagingUnits = OneUnit | TwoUnits | FourUnits
type ServiceBusNamespaceSku =
    | Basic
    | Standard
    | Premium of MessagingUnits

type ServiceBusQueueConfig =
    { NamespaceName : ResourceRef
      NamespaceSku : ServiceBusNamespaceSku
      Name : ResourceName
      LockDurationMinutes : int option
      DuplicateDetection : int option
      Session : bool option
      DeadLetteringOnMessageExpiration : bool option
      MaxDeliveryCount : int option
      EnablePartitioning : bool option
      DependsOn : ResourceName list }
    member private _.GetKeyPath sbNsName property =
        sprintf
            "listkeys(resourceId('Microsoft.ServiceBus/namespaces/authorizationRules', '%s', 'RootManageSharedAccessKey'), '2017-04-01').%s"
            sbNsName
            property
        |> ArmExpression
    member this.NamespaceDefaultConnectionString = this.GetKeyPath this.NamespaceName.ResourceName.Value "primaryConnectionString"
    member this.DefaultSharedAccessPolicyPrimaryKey = this.GetKeyPath this.NamespaceName.ResourceName.Value "primaryKey"
    interface IResourceBuilder with
        member this.BuildResources location = [
            let queue =
                  { Name = this.Name
                    LockDuration = this.LockDurationMinutes |> Option.map (sprintf "PT%dM")
                    DuplicateDetection = this.DuplicateDetection |> Option.map(fun _ -> true)
                    DuplicateDetectionHistoryTimeWindow = this.DuplicateDetection |> Option.map (sprintf "PT%dM")
                    Session = this.Session
                    DeadLetteringOnMessageExpiration = this.DeadLetteringOnMessageExpiration
                    MaxDeliveryCount = this.MaxDeliveryCount
                    EnablePartitioning = this.EnablePartitioning
                    DependsOn = [ this.NamespaceName.ResourceName ] }

            match this.NamespaceName with
            | AutomaticallyCreated namespaceName ->
                NewResource
                    { Name = namespaceName
                      Location = location
                      Sku =
                        match this.NamespaceSku with
                        | Basic -> "Basic"
                        | Standard -> "Standard"
                        | Premium _ -> "Premium"
                      Capacity =
                        match this.NamespaceSku with
                        | Basic -> None
                        | Standard -> None
                        | Premium OneUnit -> Some 1
                        | Premium TwoUnits -> Some 2
                        | Premium FourUnits -> Some 4
                      DependsOn =
                        this.DependsOn
                      Queues = [ queue ]
                    }
            | External namespaceName ->
                MergeResource(namespaceName, typeof<Namespace>, fun ns ->
                  let ns = ns :?> Namespace
                  { ns with Queues = queue :: ns.Queues } :> _)
            | AutomaticPlaceholder ->
                NotSet
        ]

type ServiceBusQueueBuilder() =
    member _.Yield _ =
        { NamespaceName = ResourceRef.AutomaticPlaceholder
          NamespaceSku = Basic
          Name = ResourceName.Empty
          LockDurationMinutes = None
          DuplicateDetection = None
          Session = None
          DeadLetteringOnMessageExpiration = None
          MaxDeliveryCount = None
          EnablePartitioning = None
          DependsOn = List.empty }
    member _.Run (state:ServiceBusQueueConfig) =
        { state with
            DependsOn = List.rev state.DependsOn
            NamespaceName =
                match state.NamespaceName with
                | AutomaticPlaceholder -> state.Name.Map(sprintf "%s-ns") |> AutomaticallyCreated
                | _ -> state.NamespaceName }

    /// The name of the namespace that holds the queue.
    [<CustomOperation "namespace_name">] member _.NamespaceName(state:ServiceBusQueueConfig, name) = { state with NamespaceName = AutomaticallyCreated (ResourceName name) }
    /// Link this queue to an existing namespace instead of creating a new one.
    [<CustomOperation "link_to_namespace">]
    member _.LinkToNamespace(state:ServiceBusQueueConfig, name) = { state with NamespaceName = External name }
    member _.LinkToNamespace(state:ServiceBusQueueConfig, config) = { state with NamespaceName = External config.NamespaceName.ResourceName }
    /// The SKU of the namespace.
    [<CustomOperation "sku">] member _.Sku(state:ServiceBusQueueConfig, sku) = { state with NamespaceSku = sku }
    /// The name of the queue.
    [<CustomOperation "name">] member _.Name(state:ServiceBusQueueConfig, name) = { state with Name = ResourceName name }
    /// The length of time that a lock can be held on a message.
    [<CustomOperation "lock_duration_minutes">] member _.LockDurationMinutes(state:ServiceBusQueueConfig, duration) = { state with LockDurationMinutes = Some duration }
    /// The maximum number of times a message can be delivered before dead lettering.
    [<CustomOperation "max_delivery_count">] member _.MaxDeliveryCount(state:ServiceBusQueueConfig, count) = { state with MaxDeliveryCount = Some count }
    /// Whether to enable duplicate detection, and if so, how long to check for.
    [<CustomOperation "duplicate_detection_minutes">] member _.DuplicateDetection(state:ServiceBusQueueConfig, maxTimeWindow) = { state with DuplicateDetection = Some maxTimeWindow }
    /// Enables session support.
    [<CustomOperation "enable_session">] member _.Session(state:ServiceBusQueueConfig) = { state with Session = Some true }
    /// Enables dead lettering of messages that expire.
    [<CustomOperation "enable_dead_letter_on_message_expiration">] member _.DeadLetteringOnMessageExpiration(state:ServiceBusQueueConfig) = { state with DeadLetteringOnMessageExpiration = Some true }
    /// Enables partition support on the queue.
    [<CustomOperation "enable_partition">] member _.EnablePartition(state:ServiceBusQueueConfig) = { state with EnablePartitioning = Some true }
    /// Adds a resource that the service bus depends on.
    [<CustomOperation "depends_on">] member _.DependsOn(state:ServiceBusQueueConfig, resourceName) = { state with DependsOn = resourceName :: state.DependsOn }

let serviceBus = ServiceBusQueueBuilder()