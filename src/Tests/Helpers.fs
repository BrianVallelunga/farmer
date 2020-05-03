[<AutoOpen>]
module TestHelpers

open Farmer
open Microsoft.Rest.Serialization

let findAzureResources<'T when 'T : null> (serializationSettings:Newtonsoft.Json.JsonSerializerSettings) (deployment:Deployment) =
    let template = deployment.Template |> Writer.TemplateGeneration.processTemplate

    template.resources
    |> Seq.map SafeJsonConvert.SerializeObject
    |> Seq.choose (fun json -> SafeJsonConvert.DeserializeObject<'T>(json, serializationSettings) |> Option.ofObj)
    |> Seq.toList

let convertResourceBuilder mapper (serializationSettings:Newtonsoft.Json.JsonSerializerSettings) (resourceBuilder:IResourceBuilder) =
    resourceBuilder.BuildResources NorthEurope
    |> List.pick(function
        | MergeResource _
        | NotSet ->
            None
        | NewResource r ->
            r.ToArmObject()
            |> SafeJsonConvert.SerializeObject
            |> SafeJsonConvert.DeserializeObject
            |> mapper
            |> SafeJsonConvert.SerializeObject
            |> fun json -> SafeJsonConvert.DeserializeObject(json, serializationSettings)
            |> Option.ofObj
    )