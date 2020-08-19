(*
    PopupView.fs

    This module provides a handy interface to create popups and notifications.
    Popups and notifications appear similar, but are actually quite different:
    - Model.Popup is a function that takes a STRING and produces a ReactElement.
    - Model.Notifications are a functions that take DISPATCH and produce a
      ReactElement.
    This means that at the moment of creation, a popup must already have the
    dispatch function, while the notification does not. This, in turn, means
    that notifications can be created from messages dispatched by JS code.
*)

module PopupView

open Fulma
open Fable.React
open Fable.React.Props

open JSHelpers
open Helpers
open DiagramMessageType
open DiagramModelType
open CommonTypes
open DiagramStyle

//=======//
//HELPERS//
//=======//

let extractLabelBase (text:string) : string =
    text.ToUpper()
    |> Seq.takeWhile (fun ch -> ch <> '(')
    |> Seq.filter System.Char.IsLetterOrDigit
    |> Seq.map (fun ch -> ch.ToString())
    |> String.concat ""

let formatLabelAsBus (width:int) (text:string) =
    let text' = extractLabelBase text
    match width with
    | 1 -> text'
    | _ -> sprintf "%s(%d:%d)" text' (width-1) 0
   

let formatLabelFromType compType (text:string) =
    let text' = extractLabelBase text
    match compType with
    | Input 1 | Output 1 -> text'
    | Input width | Output width -> sprintf "%s(%d:%d)" text' (width-1) 0
    | _ -> text'


let formatLabel comp (text:string) =
    formatLabelFromType comp.Type (text:string)

let setComponentLabel model comp text =
    let label = formatLabel comp text
    printf "Setting label %s" label
    model.Diagram.EditComponentLabel comp.Id label

let setComponentLabelFromText model (comp:Component) text =
    printf "Setting label %s" text
    model.Diagram.EditComponentLabel comp.Id text

//========//
// Popups //
//========//

let getText (dialogData : PopupDialogData) =
    Option.defaultValue "" dialogData.Text

let getInt (dialogData : PopupDialogData) =
    Option.defaultValue 1 dialogData.Int

let getInt2 (dialogData : PopupDialogData) =
    Option.defaultValue 0 dialogData.Int2

let getMemorySetup (dialogData : PopupDialogData) =
    Option.defaultValue (1,1) dialogData.MemorySetup

let getMemoryEditor (dialogData : PopupDialogData) =
    Option.defaultValue
        { Address = None; OnlyDiff = false; NumberBase = Hex }
        dialogData.MemoryEditorData

/// Unclosable popup.
let unclosablePopup maybeTitle body maybeFoot extraStyle =
    let head =
        match maybeTitle with
        | None -> div [] []
        | Some title -> Modal.Card.head [] [ Modal.Card.title [] [ str title ] ]
    let foot =
        match maybeFoot with
        | None -> div [] []
        | Some foot -> Modal.Card.foot [] [ foot ]
    Modal.modal [ Modal.IsActive true ] [
        Modal.background [] []
        Modal.Card.card [Props [Style extraStyle]] [
            head
            Modal.Card.body [] [ body ]
            foot
        ]
    ]

let showMemoryEditorPopup maybeTitle body maybeFoot extraStyle dispatch =
    fun dialogData ->
        let memoryEditorData = getMemoryEditor dialogData
        unclosablePopup maybeTitle (body memoryEditorData) maybeFoot extraStyle
    |> ShowPopup |> dispatch

let private buildPopup title body foot close extraStyle =
    fun (dialogData : PopupDialogData) ->
        Modal.modal [ Modal.IsActive true ] [
            Modal.background [ Props [ OnClick close ] ] []
            Modal.Card.card [ Props [Style extraStyle] ] [
                Modal.Card.head [] [
                    Modal.Card.title [] [ str title ]
                    Delete.delete [ Delete.OnClick close ] []
                ]
                Modal.Card.body [] [ body dialogData ]
                Modal.Card.foot [] [ foot dialogData ]
            ]
        ]

/// Body and foot are functions that take a string of text and produce a
/// reactElement. The meaning of the input string to those functions is the
/// content of PopupDialogText (i.e. in a dialog popup, the string is the
/// current value of the input box.).
let private dynamicClosablePopup title body foot extraStyle dispatch =
    buildPopup title body foot (fun _ -> dispatch ClosePopup) extraStyle
    |> ShowPopup |> dispatch

/// Create a popup and add it to the page. Body and foot are static content.
/// Can be closed by the ClosePopup message.
let closablePopup title body foot extraStyle dispatch =
    dynamicClosablePopup title (fun _ -> body) (fun _ -> foot) extraStyle dispatch

/// Create the body of a dialog Popup with only text.
let dialogPopupBodyOnlyText before placeholder dispatch =
    fun (dialogData : PopupDialogData) ->
        div [] [
            before dialogData
            Input.text [
                Input.Props [AutoFocus true; SpellCheck false]
                Input.Placeholder placeholder
                Input.OnChange (getTextEventValue >> Some >> SetPopupDialogText >> dispatch)
            ]
        ]

/// Create the body of a dialog Popup with only an int.
let dialogPopupBodyOnlyInt beforeInt intDefault dispatch =
    intDefault |> Some |> SetPopupDialogInt |> dispatch
    fun (dialogData : PopupDialogData) ->
        div [] [
            beforeInt dialogData
            br []
            Input.number [
                Input.Props [Style [Width "60px"]; AutoFocus true]
                Input.DefaultValue <| sprintf "%d" intDefault
                Input.OnChange (getIntEventValue >> Some >> SetPopupDialogInt >> dispatch)
            ]
        ]
/// Create the body of a dialog Popup with two ints.
let dialogPopupBodyTwoInts (beforeInt1,beforeInt2) (intDefault1,intDefault2) dispatch =

    let setPopupTwoInts (whichInt:IntMode) =
        fun n -> (Some n, whichInt) |> SetPopupDialogTwoInts |> dispatch

    setPopupTwoInts FirstInt intDefault1 
    setPopupTwoInts SecondInt intDefault2 

    fun (dialogData : PopupDialogData) ->
        div [] [
            beforeInt1 dialogData
            br []
            Input.number [
                Input.Props [Style [Width "60px"]; AutoFocus true]
                Input.DefaultValue <| sprintf "%d" intDefault1
                Input.OnChange (getIntEventValue >> setPopupTwoInts FirstInt)
            ]
            br []
            beforeInt2 dialogData
            br []
            Input.number [
                Input.Props [Style [Width "60px"]; AutoFocus true]
                Input.DefaultValue <| sprintf "%d" intDefault2
                Input.OnChange (getIntEventValue >> setPopupTwoInts SecondInt)
            ]
        ]

/// Create the body of a dialog Popup with both text and int.
let dialogPopupBodyTextAndInt beforeText placeholder beforeInt intDefault dispatch =
    intDefault |> Some |> SetPopupDialogInt |> dispatch
    fun (dialogData : PopupDialogData) ->
        div [] [
            beforeText dialogData
            Input.text [
                Input.Props [AutoFocus true; SpellCheck false]
                Input.Placeholder placeholder
                Input.OnChange (getTextEventValue >> Some >> SetPopupDialogText >> dispatch)
            ]
            br []
            br []
            beforeInt dialogData
            br []
            Input.number [
                Input.Props [Style [Width "60px"]]
                Input.DefaultValue <| sprintf "%d" intDefault
                Input.OnChange (getIntEventValue >> Some >> SetPopupDialogInt >> dispatch)
            ]
        ]

/// Create the body of a memory dialog popup: asks for AddressWidth and
/// WordWidth, two integers.
let dialogPopupBodyMemorySetup intDefault dispatch =
    Some (4, intDefault) 
    |> SetPopupDialogMemorySetup |> dispatch
    fun (dialogData : PopupDialogData) ->
        let addressWidth, wordWidth = getMemorySetup dialogData
        div [] [
            str "How many bits should be used to address the data in memory?"
            br []
            str <| sprintf "%d bits yield %d memory locations." addressWidth (pow2int64 addressWidth)
            br []
            Input.number [
                Input.Props [Style [Width "60px"] ; AutoFocus true]
                Input.DefaultValue (sprintf "%d" 4)
                Input.OnChange (getIntEventValue >> fun newAddrWidth ->
                    Some (newAddrWidth, wordWidth) 
                    |> SetPopupDialogMemorySetup |> dispatch
                )
            ]
            br []
            br []
            str "How many bits should each memory word contain?"
            br []
            Input.number [
                Input.Props [Style [Width "60px"]]
                Input.DefaultValue (sprintf "%d" intDefault)
                Input.OnChange (getIntEventValue >> fun newWordWidth ->
                    Some (addressWidth, newWordWidth) 
                    |> SetPopupDialogMemorySetup |> dispatch
                )
            ]
            br []
            br []
            str "You will be able to set the content of the memory from the Component Properties menu."
        ]

/// Popup with an input textbox and two buttons.
/// The text is reflected in Model.PopupDialogText.
let dialogPopup title body buttonText buttonAction isDisabled dispatch =
    let foot =
        fun (dialogData : PopupDialogData) ->
            Level.level [ Level.Level.Props [ Style [ Width "100%" ] ] ] [
                Level.left [] []
                Level.right [] [
                    Level.item [] [
                        Button.button [
                            Button.Color IsLight
                            Button.OnClick (fun _ -> dispatch ClosePopup)
                        ] [ str "Cancel" ]
                    ]
                    Level.item [] [
                        Button.button [
                            Button.Disabled (isDisabled dialogData)
                            Button.Color IsPrimary
                            Button.OnClick (fun _ -> buttonAction dialogData)
                        ] [ str buttonText ]
                    ]
                ]
            ]
    dynamicClosablePopup title body foot [] dispatch

/// A static confirmation popup.
let confirmationPopup title body buttonText buttonAction dispatch =
    let foot =
        Level.level [ Level.Level.Props [ Style [ Width "100%" ] ] ] [
            Level.left [] []
            Level.right [] [
                Level.item [] [
                    Button.button [
                        Button.Color IsLight
                        Button.OnClick (fun _ -> dispatch ClosePopup)
                    ] [ str "Cancel" ]
                ]
                Level.item [] [
                    Button.button [
                        Button.Color IsPrimary
                        Button.OnClick buttonAction
                    ] [ str buttonText ]
                ]
            ]
        ]
    closablePopup title body foot [] dispatch

/// Display popup, if any is present.
let viewPopup model =
    match model.Popup with
    | None -> div [] []
    | Some popup -> popup model.PopupDialogData

//===============//
// Notifications //
//===============//

let errorNotification text closeMsg =
    fun dispatch ->
        let close = (fun _ -> dispatch closeMsg)
        Notification.notification [
            Notification.Color IsDanger
            Notification.Props [ notificationStyle ]
        ] [
            Delete.delete [ Delete.OnClick close ] []
            str text
        ]

let viewNotifications model dispatch =
    [ model.Notifications.FromDiagram
      model.Notifications.FromSimulation
      model.Notifications.FromFiles
      model.Notifications.FromMemoryEditor
      model.Notifications.FromProperties ]
    |> List.tryPick id
    |> function
    | Some notification -> notification dispatch
    | None -> div [] []
