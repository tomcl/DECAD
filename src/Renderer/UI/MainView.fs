﻿module DiagramMainView

open Fulma
open Fable.React
open Fable.React.Props

open BusWidthInferer
open BusTypes

open DiagramStyle
open SimulatorTypes
open DiagramMessageType
open DiagramModelType
open CommonTypes
open Draw2dWrapper
open Extractor
open OnDiagramButtonsView
open CatalogueView
open SelectedComponentView
open SimulationView
open PopupView
open FileMenuView
open WaveformSimulationView



// -- Init Model

let init() = {
    Diagram = new Draw2dWrapper()
    SelectedComponent = None
    LastUsedDialogWidth = 1
    Simulation = None
    WaveSim = WaveformSimulationView.initModel
    RightTab = Catalogue
    CurrProject = None
    Hilighted = [], []
    Clipboard = [], []
    CreateComponent = None
    HasUnsavedChanges = false
    Popup = None
    PopupDialogData = {
        Text = None
        Int = None
        Int2 = None
        MemorySetup = None
        MemoryEditorData = None
    }
    Notifications = {
        FromDiagram = None
        FromSimulation = None
        FromWaveSim = None
        FromFiles = None
        FromMemoryEditor = None
        FromProperties = None
    }
    TopMenu = Closed
    DragMode = DragModeOff
    ViewerWidth = rightSectionWidthViewerDefault
}

/// Repaint each connection according to the new inferred width.
let private repaintConnections model connsWidth =
    connsWidth
    |> Map.map (fun (BusTypes.ConnectionId connId) width ->
        match width with
        | None -> () // Could not infer.
        | Some w -> model.Diagram.PaintConnection connId w
    )
    |> ignore

let private mapPortIdsToConnWidth conns connsWidth = 
    (Map.empty, conns) ||> List.fold (fun map conn ->
        let width = getConnectionWidth connsWidth <| BusTypes.ConnectionId conn.Id
        let map = map.Add (conn.Source.Id, width)
        map.Add (conn.Target.Id, width)
    )

let private repaintBusComponents model connsWidth state =
    let comps, conns = state
    let portIdsToConnWidth = mapPortIdsToConnWidth conns connsWidth
    let lookupWidthOnPort portId =
        match portIdsToConnWidth.TryFind portId with
        | None -> None // Unconnected.
        | Some w -> w // Connected, may be inferred or not.
    comps |> List.map (fun comp ->
        match comp.Type with
        | MergeWires ->
            (
                comp.InputPorts.[0].Id |> lookupWidthOnPort, // Top left input wire.
                comp.InputPorts.[1].Id |> lookupWidthOnPort, // Bottom left input wire.
                comp.OutputPorts.[0].Id |> lookupWidthOnPort // Output wire.
            ) |||> model.Diagram.UpdateMergeWiresLabels comp.Id
        | SplitWire topOutputWidth ->
            (
                comp.InputPorts.[0].Id |> lookupWidthOnPort,  // Input wire.
                Some topOutputWidth, // Top right input wire.
                comp.OutputPorts.[1].Id |> lookupWidthOnPort  // Bottom right input wire.
            ) |||> model.Diagram.UpdateSplitWireLabels comp.Id
        | _ -> () // Ignore other components.
    ) |> ignore

let private runBusWidthInference model =
    match model.Diagram.GetCanvasState () with
    | None -> model
    | Some jsState ->
        let state = extractState jsState
        state |> inferConnectionsWidth |> function
        | Error e ->
            // TODO: this makes the content of the model.Higlighted inconsistent.
            // Need to dispatch SetHighlighted (can do by using mkProgram).
            e.ConnectionsAffected
            |> List.map (fun (BusTypes.ConnectionId c) -> model.Diagram.HighlightConnection c)
            |> ignore
            // Display notification with error message.
            { model with Notifications =
                            { model.Notifications with FromDiagram =
                                                        Some <| errorNotification e.Msg CloseDiagramNotification} }
        | Ok connsWidth ->
            repaintConnections model connsWidth
            repaintBusComponents model connsWidth state
            // Close the notification if all is good.
            { model with Notifications = {model.Notifications with FromDiagram = None} }





// -- Create View

/// Display the content of the right tab.
let private viewRightTab model dispatch =
    match model.RightTab with
    | Catalogue ->
        div [ Style [Width "90%"; MarginLeft "5%"; MarginTop "15px" ] ] [
            Heading.h4 [] [ str "Catalogue" ]
            div [ Style [ MarginBottom "15px" ] ] [ str "Click on a component to add it to the diagram." ]
            viewCatalogue model dispatch
        ]
    | Properties ->
        div [ Style [Width "90%"; MarginLeft "5%"; MarginTop "15px" ] ] [
            Heading.h4 [] [ str "Component properties" ]
            viewSelectedComponent model dispatch
        ]
    | Simulation ->
        div [ Style [Width "90%"; MarginLeft "5%"; MarginTop "15px" ] ] [
            Heading.h4 [] [ str "Simulation" ]
            viewSimulation model dispatch
        ]
    | WaveSim -> 
        div [ Style [Width "calc(100% - 10px)"; Height "95.5%"; MarginLeft "0%"; MarginTop "0px"; OverflowX OverflowOptions.Hidden; OverflowY OverflowOptions.Hidden ] ] 
            (viewWaveSim model dispatch) 

let setDragMode (modeIsOn:bool) (model:Model) dispatch =
    fun (ev: Browser.Types.MouseEvent) ->
        //printfn "START X=%d, buttons=%d, mode=%A, width=%A, " (int ev.clientX) (int ev.buttons) model.DragMode model.ViewerWidth
        match modeIsOn, model.DragMode with
        | true, DragModeOff ->  SetDragMode (DragModeOn (int ev.clientX)) |> dispatch
        | false, DragModeOn _ -> SetDragMode DragModeOff |> dispatch
        | _ -> ()


let dividerbar (model:Model) dispatch =
    let isDraggable = model.RightTab = WaveSim
    let variableStyle = 
        if isDraggable then [
            BackgroundColor "gold"
            Cursor "col-resize" 
            Width "10px"

        ] else [
            BackgroundColor "lightgrey"
            Width "2px"

        ]
    let commonStyle = [
            Height "100%"
            Float FloatOptions.Left
        ]
    div [
            Style <| commonStyle @ variableStyle
            OnMouseDown (setDragMode true model dispatch)       
        ] []

let displayView model dispatch =
    let windowX,windowY =
        int Browser.Dom.self.innerWidth, int Browser.Dom.self.innerHeight
    let selectedComps, selectedconns = 
        model.Diagram.GetSelected()
        |> Option.map extractState
        |> Option.defaultValue ([],[])
    let sd = scrollData model
    let x' = sd.SheetLeft+sd.SheetX
    let y' = sd.SheetTop+sd.SheetY
    //selectedComps
    //|> List.map (fun comp -> sprintf "(%d,%d)" comp.X  comp.Y )
    //|> String.concat ","
    //|> (fun comps -> printfn "W=(%d,%d) Top=(%d,%d) Bot=(%d,%d)Comps=[%s]\n%A\n\n"  windowX windowY sd.SheetLeft sd.SheetTop x' y' comps sd)
  


    let processMouseMove (ev: Browser.Types.MouseEvent) =
        //printfn "X=%d, buttons=%d, mode=%A, width=%A, " (int ev.clientX) (int ev.buttons) model.DragMode model.ViewerWidth
        match model.DragMode, ev.buttons with
        | DragModeOn pos , 1.-> 
            let newWidth = model.ViewerWidth - int ev.clientX + pos
            let w = 
                newWidth
                |> max DiagramStyle.minViewerWidth
                |> min (windowX - DiagramStyle.minEditorWidth)
            SetViewerWidth w |> dispatch
            SetDragMode (DragModeOn (int ev.clientX)) |> dispatch
        | DragModeOn _, _ ->  SetDragMode DragModeOff |> dispatch
        | DragModeOff, _-> ()

    div [
            OnMouseUp (setDragMode false model dispatch);
            OnMouseMove processMouseMove
    ] [
        viewTopMenu model dispatch 
        model.Diagram.CanvasReactElement (JSDiagramMsg >> dispatch) (canvasVisibleStyle model |> DispMode ) 
        viewNoProjectMenu model dispatch
        viewPopup model
        viewNotifications model dispatch
        viewOnDiagramButtons model dispatch
        div [ rightSectionStyle model ;  ] [
            dividerbar model dispatch
            div [Style [Height "100%"]] [
            Tabs.tabs [ Tabs.IsFullWidth; Tabs.IsBoxed; Tabs.Props [ Style [FontSize "80%"]  ] ] [
                Tabs.tab
                    [ Tabs.Tab.IsActive (model.RightTab = Catalogue) ]
                    [ a [ OnClick (fun _ -> ChangeRightTab Catalogue |> dispatch ) ] [ str "Catalogue" ] ]
                Tabs.tab
                    [ Tabs.Tab.IsActive (model.RightTab = Properties) ]
                    [ a [ OnClick (fun _ -> ChangeRightTab Properties |> dispatch ) ] [ str "Properties" ] ]
                Tabs.tab
                    [ Tabs.Tab.IsActive (model.RightTab = Simulation) ]
                    [ a [ OnClick (fun _ -> ChangeRightTab Simulation |> dispatch ) ] [ str "Simulation" ] ]
                Tabs.tab
                    [ Tabs.Tab.IsActive (model.RightTab = WaveSim) ]
                    [ a [ OnClick (fun _ -> 
                                        ChangeRightTab WaveSim |> dispatch
                                        Ok WaveformSimulationView.initModel |> StartWaveSim |> dispatch) ]
                        [ str "WaveSim" ] ]
            ]
            viewRightTab model dispatch
            ]
        ]
    ]

// -- Update Model

let private getSimulationDataOrFail model msg =
    match model.Simulation with
    | None -> failwithf "what? Getting simulation data when no simulation is running: %s" msg
    | Some sim ->
        match sim with
        | Error _ -> failwithf "what? Getting simulation data when could not start because of error: %s" msg
        | Ok simData -> simData

// Handle messages triggered by the JS diagram.
let private handleJSDiagramMsg msg model =
    match msg with
    | InitCanvas canvas -> // Should be triggered only once.
        model.Diagram.InitCanvas canvas
        model
    | SelectComponent jsComponent ->
        { model with SelectedComponent = Some <| extractComponent jsComponent }
    | UnselectComponent () ->
        { model with SelectedComponent = None }
    | InferWidths () ->
        runBusWidthInference model
    | SetHasUnsavedChanges s ->
        { model with HasUnsavedChanges = s }

// Handle messages triggered by keyboard shortcuts.
let private handleKeyboardShortcutMsg msg model =
    match msg with
    | CtrlS ->
        saveOpenFileAction model
        { model with HasUnsavedChanges = false }
    | AltC ->
        // Similar to the body of OnDiagramButtonsView.copyAction but without
        // dispatching the SetClipboard message.
        match model.Diagram.GetSelected () with
        | None -> model
        | Some jsState -> { model with Clipboard = extractState jsState }
    | AltV ->
        pasteAction model
        model
    | AltZ ->
        model.Diagram.Undo ()
        model
    | AltShiftZ ->
        model.Diagram.Redo ()
        model

let getMenuView (act: MenuCommand) (model: Model) (dispatch: Msg -> Unit) =
    match act with
    | MenuPrint ->
        match model.CurrProject with
        | None -> ()
        | Some p ->
            model.Diagram.printCanvas( fun (fn:string) (png:string) -> 
                printfn "PNG is %d bytes" png.Length
                FilesIO.savePngFile p.ProjectPath p.OpenFileName  png)
    | MenuSaveFile -> 
        FileMenuView.saveOpenFileAction model 
        SetHasUnsavedChanges false
        |> JSDiagramMsg |> dispatch
    | MenuNewFile -> 
        FileMenuView.addFileToProject model dispatch
    | MenuZoom z -> 
        zoomDiagram z model
    model
    

let update msg model =
    //let inP f = Option.map f model.CurrProject
    //printfn "UPDATE: %A (dirty=%A, project=%A)" msg model.HasUnsavedChanges (inP (fun p -> p.ProjectPath))
    match msg with
    | SetDragMode mode -> {model with DragMode= mode}
    | SetViewerWidth w -> {model with ViewerWidth = w}
    | JSDiagramMsg msg' -> handleJSDiagramMsg msg' model
    | KeyboardShortcutMsg msg' -> handleKeyboardShortcutMsg msg' model
    // Messages triggered by the "classic" Elmish UI (e.g. buttons and so on).
    | StartSimulation simData -> { model with Simulation = Some simData }
    | StartWaveSim msg -> 
        match msg with
        | Ok wsData -> { model with WaveSim = wsData }
        | Error err -> { model with Simulation = Error err |> Some }
    | SetSimulationGraph graph ->
        let simData = getSimulationDataOrFail model "SetSimulationGraph"
        { model with Simulation = { simData with Graph = graph } |> Ok |> Some }
    | SetSimulationBase numBase ->
        let simData = getSimulationDataOrFail model "SetSimulationBase"
        { model with Simulation = { simData with NumberBase = numBase } |> Ok |> Some }
    | IncrementSimulationClockTick ->
        let simData = getSimulationDataOrFail model "IncrementSimulationClockTick"
        { model with Simulation = { simData with ClockTickNumber = simData.ClockTickNumber+1 } |> Ok |> Some }
    | EndSimulation -> { model with Simulation = None }
    | ChangeRightTab newTab -> { model with RightTab = newTab }
    | SetHighlighted (componentIds, connectionIds) ->
        let oldComponentIds, oldConnectionIds = model.Hilighted
        oldComponentIds
        |> List.map (fun (ComponentId c) -> model.Diagram.UnHighlightComponent c)
        |> ignore
        componentIds
        |> List.map (fun (ComponentId c) -> model.Diagram.HighlightComponent Red c)
        |> ignore
        oldConnectionIds
        |> List.map (fun (ConnectionId c) -> model.Diagram.UnHighlightConnection c)
        |> ignore
        connectionIds
        |> List.map (fun (ConnectionId c) -> model.Diagram.HighlightConnection c)
        |> ignore
        { model with Hilighted = (componentIds, connectionIds) }
    | SetClipboard components -> { model with Clipboard = components }
    | SetCreateComponent pos -> { model with CreateComponent = Some pos }
    | SetProject project -> { model with CurrProject = Some project }
    | CloseProject -> { model with CurrProject = None }
    | ShowPopup popup -> { model with Popup = Some popup }
    | ClosePopup ->
        { model with Popup = None; PopupDialogData =
                    { Text = None; Int = None; Int2 = None; MemorySetup = None; MemoryEditorData = None} }
    | SetPopupDialogText text ->
        { model with PopupDialogData = {model.PopupDialogData with Text = text} }
    | SetPopupDialogInt int ->
        { model with PopupDialogData = {model.PopupDialogData with Int = int} }
    | SetPopupDialogTwoInts data ->
        { model with PopupDialogData = 
                        match data with
                        | n, FirstInt ->  {model.PopupDialogData with Int  = n}
                        | n, SecondInt -> {model.PopupDialogData with Int2 = n}
        }
    | SetPopupDialogMemorySetup m ->
        { model with PopupDialogData = {model.PopupDialogData with MemorySetup = m} }
    | SetPopupMemoryEditorData m ->
        { model with PopupDialogData = {model.PopupDialogData with MemoryEditorData = m} }
    | CloseDiagramNotification ->
        { model with Notifications = {model.Notifications with FromDiagram = None} }
    | SetSimulationNotification n ->
        { model with Notifications =
                        { model.Notifications with FromSimulation = Some n} }
    | CloseSimulationNotification ->
        { model with Notifications = {model.Notifications with FromSimulation = None} }
    | CloseWaveSimNotification ->
        { model with Notifications = {model.Notifications with FromWaveSim = None} }
    | SetFilesNotification n ->
        { model with Notifications =
                        { model.Notifications with FromFiles = Some n} }
    | CloseFilesNotification ->
        { model with Notifications = {model.Notifications with FromFiles = None} }
    | SetMemoryEditorNotification n ->
        { model with Notifications =
                        { model.Notifications with FromMemoryEditor = Some n} }
    | CloseMemoryEditorNotification ->
        { model with Notifications = { model.Notifications with FromMemoryEditor = None} }
    | SetPropertiesNotification n ->
        { model with Notifications =
                        { model.Notifications with FromProperties = Some n} }
    | ClosePropertiesNotification ->
        { model with Notifications = { model.Notifications with FromProperties = None} }
    | SetTopMenu t ->
        { model with TopMenu = t}    
    | ReloadSelectedComponent width ->
        match model.SelectedComponent with
        | None -> {model with LastUsedDialogWidth = width}
        | Some comp ->
            match model.Diagram.GetComponentById comp.Id with
            | Error err -> {model with LastUsedDialogWidth=width}
            | Ok jsComp -> { model with SelectedComponent = Some <| extractComponent jsComp ; LastUsedDialogWidth=width}
    | MenuAction(act,dispatch) ->
        getMenuView act model dispatch
