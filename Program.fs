﻿open System.IO
open System.Diagnostics

open Argu

open OpenQA.Selenium
open OpenQA.Selenium.Support.UI
open OpenQA.Selenium.Firefox

// Command line arguments definition
type Arguments =
    | Working_Directory of path:string
    | Output_Directory of path:string
    | URL of uri:string
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Working_Directory _ -> "specify a working directory. (optional, default is current directory)"
            | Output_Directory _ -> "specify an output directory. (optional, default is current directory)"
            | URL _ -> "specify the base URL of your FsReveal slideshow. (Optional, default is http://127.0.0.1:8083/)"

let parser = ArgumentParser.Create<Arguments>(programName = "FsRevealSlidePrinter.exe")
let args = parser.ParseCommandLine()

let print (driver:WebDriver) url outputFile =
    
    printfn "Navigating Firefox to %s" url
    driver.Navigate().GoToUrl(url)

    // Wait for end of page load, i.e. body has the print-pdf class
    printfn "Waiting for page to load fully"
    let wait = new WebDriverWait(driver, System.TimeSpan.FromMinutes(1));
    let sw = Stopwatch.StartNew()
    let result = wait.Until(fun d -> d.FindElement(By.TagName("body")).GetAttribute("class") = "print-pdf")
    printfn "Waited %A for page to load" sw.Elapsed
    printfn "Wait 3 more seconds to be sure all plugins have finished loading"
    let waitMore = System.TimeSpan.FromSeconds(3)
    System.Threading.Thread.Sleep(waitMore)

    if result then
        let printOptions = new PrintOptions()
        printOptions.Orientation <- PrintOrientation.Landscape
        // Hardcoded A4 landscape paper size
        printOptions.PageDimensions.Height <- 29.7
        printOptions.PageDimensions.Width <- 21.0
        printfn "Printing file"
        let doc = driver.Print(printOptions)
        printfn "Saving file to %s" outputFile
        doc.SaveAsFile(outputFile)
    else
        printfn "Page load timed out."

let workdir = args.GetResult(Working_Directory)

if System.IO.Directory.Exists(workdir) |> not then
    printfn "Working directory %s does not exist" workdir
    System.Environment.Exit(-1)

let outdir = args.GetResult(Output_Directory, System.Environment.CurrentDirectory)
if System.IO.Directory.Exists(outdir) |> not then
    printfn "Creating missing output directory %s" outdir
    System.IO.Directory.CreateDirectory(outdir) |> ignore

let mutable baseUrl = args.GetResult(URL,  "http://127.0.0.1:8083/")
// Quick fix for missing slash in base URL, would be better to use an actual URL builder
if not (baseUrl.EndsWith("/")) then
    baseUrl <- baseUrl + "/"

let driver = new FirefoxDriver()
let globalSW = Stopwatch.StartNew()

System.IO.Directory.GetFiles(workdir, "*.md")
|> Seq.map Path.GetFileNameWithoutExtension
// Custom filter my personal use, would be nice to have a filter command line arg with regex or something like that ?
|> Seq.filter (fun f -> System.Char.IsDigit(f.[0])) 
|> Seq.iter (fun f -> 
    printfn "Printing slides from file : %s" f
    let url = baseUrl + f + ".html?print-pdf=1"
    let outputFile = Path.Combine(outdir, f + ".pdf")
    let sw = Stopwatch.StartNew()
    print driver url outputFile
    printfn "Export took %A" sw.Elapsed
    )

printfn "All exports done in %A" globalSW.Elapsed



