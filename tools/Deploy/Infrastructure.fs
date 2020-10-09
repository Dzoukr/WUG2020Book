module Deploy.Infrastructure

open Farmer
open Farmer.Builders
open Fake.IO.FileSystemOperators

let getDeployment (appPath:string) =

    // appinsights
    let insights = appInsights {
        name "appinsights-wug2020"
    }

    // storage account
    let storage = storageAccount {
        name "storagewug2020"
        sku Storage.Standard_LRS
    }

    // web app
    let web = webApp {
        name "wug2020book"
        service_plan_name "myServicePlan"
        setting "StorageConnectionString" storage.Key.Value
        sku WebApp.Sku.B1
        always_on
        link_to_app_insights insights.Name
    }

    arm {
        location Location.WestEurope
        add_resource insights
        add_resource storage
        add_resource web
    }