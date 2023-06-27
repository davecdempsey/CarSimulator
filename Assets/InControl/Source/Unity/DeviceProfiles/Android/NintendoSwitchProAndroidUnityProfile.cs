// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedType.Global
namespace InControl.UnityDeviceProfiles
{
	// @cond nodoc
	[Preserve] [UnityInputDeviceProfile]
	public class NintendoSwitchProAndroidUnityProfile : InputDeviceProfile
	{
		public override void Define()
		{
			base.Define();

			DeviceName = "Nintendo Switch Pro Controller";
			DeviceNotes = "Nintendo Switch Pro Controller on Android";

			DeviceClass = InputDeviceClass.Controller;
			DeviceStyle = InputDeviceStyle.NintendoSwitch;

			IncludePlatforms = new[]
			{
				"Android"
			};

			Matchers = new[] { new InputDeviceMatcher { NameLiteral = "Pro Controller" } };

			ButtonMappings = new[]
			{
				new InputControlMapping
				{
					Name = "A",
					Target = InputControlType.Action2,
					Source = Button( 1 )
				},
				new InputControlMapping
				{
					Name = "B",
					Target = InputControlType.Action1,
					Source = Button( 0 )
				},
				new InputControlMapping
				{
					Name = "X",
					Target = InputControlType.Action4,
					Source = Button( 3 )
				},
				new InputControlMapping
				{
					Name = "Y",
					Target = InputControlType.Action3,
					Source = Button( 2 )
				},
				new InputControlMapping
				{
					Name = "Select",
					Target = InputControlType.Select,
					Source = Button( 11 )
				},
				new InputControlMapping
				{
					Name = "Start",
					Target = InputControlType.Start,
					Source = Button( 10 )
				},
				new InputControlMapping
				{
					Name = "L",
					Target = InputControlType.LeftBumper,
					Source = Button( 4 )
				},
				new InputControlMapping
				{
					Name = "R",
					Target = InputControlType.RightBumper,
					Source = Button( 5 )
				},
				new InputControlMapping
				{
					Name = "ZL",
					Target = InputControlType.LeftTrigger,
					Source = Button( 6 )
				},
				new InputControlMapping
				{
					Name = "ZR",
					Target = InputControlType.RightTrigger,
					Source = Button( 7 )
				},
				new InputControlMapping
				{
					Name = "Left Stick Button",
					Target = InputControlType.LeftStickButton,
					Source = Button( 8 )
				},
				new InputControlMapping
				{
					Name = "Right Stick Button",
					Target = InputControlType.RightStickButton,
					Source = Button( 9 )
				}
			};

			AnalogMappings = new[]
			{
				LeftStickLeftMapping( 0 ),
				LeftStickRightMapping( 0 ),
				LeftStickUpMapping( 1 ),
				LeftStickDownMapping( 1 ),

				RightStickLeftMapping( 2 ),
				RightStickRightMapping( 2 ),
				RightStickUpMapping( 3 ),
				RightStickDownMapping( 3 ),

				DPadLeftMapping( 4 ),
				DPadRightMapping( 4 ),
				DPadUpMapping( 5 ),
				DPadDownMapping( 5 )
			};
		}
	}

	// @endcond
}
