﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetProcGame;
using NetProcGame.game;
using NetProcGame.modes;
using NetProcGame.lamps;
using NetProcGame.tools;

namespace PinprocTest.StarterGame
{
    public class StarterGame : BasicGame
    {
        public Attract attract;
        public BaseGameMode _base_game_mode;
        public BallSave ball_save;
        public Trough trough;
        public LampController lampctrl;

        public bool ball_being_saved = false;

		public I2cServo wall1;
		public I2cServo wall2;
		public I2cServo testServo;
		public I2cServo flasherMotor1;
		public I2cServo flasherMotor2;
		public I2cServo flasherMotor3;
		public WSLEDDriver ledDriver;

        public StarterGame(ILogger logger)
			: base(MachineType.PDB, logger, false)
        {
            this.lampctrl = new LampController(this);
			// Internal offset should be 200
			this.ledDriver = new WSLEDDriver ("/dev/tty.usbmodem1403131", 50);
        }

        public void save_settings()
        {
        }

        public void setup()
        {
            LoadConfig(@"configuration/machine.json");
            setup_ball_search();

            this.all_gi_on();

            // Lamp showage
            //this.lampctrl.register_show("attract1", @"lamps\attract1.lampshow");
            this.lampctrl.register_show("attract2", @"lamps/attract2.lampshow");
            //this.lampctrl.register_show("attract3", @"lamps\attract3.lampshow");
            //this.lampctrl.register_show("attract4", @"lamps\attract4.lampshow");

            // Intantiate basic game features
            attract = new Attract(this);
            _base_game_mode = new BaseGameMode(this);

            string[] trough_switchnames = new string[3] { "trough1", "trough2", "trough3" };
            trough = new Trough(this,
                                trough_switchnames,
                                "trough1",
                                "trough",
                                new string[] { "leftOutlane", "rightOutlane" },
                                "shooterLane");
            ball_save = new BallSave(this, "ballSave", "shooterLane");

            // Link ball save to trough
            ball_save.allow_multiple_saves = false;
            trough.ball_save_callback = new AnonDelayedHandler(ball_save.launch_callback);
            trough.num_balls_to_save = new GetNumBallsToSaveHandler(ball_save.get_num_balls_to_save);
            ball_save.trough_enable_ball_save = new BallSaveEnable(trough.enable_ball_save);

			this.PROC.initialize_i2c (0x40);

			ServoConfiguration _servoConfig = new ServoConfiguration () { address = 0x40, minimum = 150, maximum = 600 };

			this.wall1 = new I2cServo(0, _servoConfig, this.PROC);
			this.wall2 = new I2cServo(1, _servoConfig, this.PROC);
			this.flasherMotor1 = new I2cServo(2, _servoConfig, this.PROC);
			this.flasherMotor2 = new I2cServo(3, _servoConfig, this.PROC);
			this.flasherMotor3 = new I2cServo(4, _servoConfig, this.PROC);
			this.testServo = new I2cServo (0, _servoConfig, this.PROC);

			this.ledDriver.FadeAllToColor (0, 0, 0, 0);
			this.ledDriver.ScheduleAll (0xFFFFFFFF);
			Thread.Sleep (1000);
			this.ledDriver.FadeAllToColor (255, 0, 0, 32);
            
            // Instead of resetting everything here as well as when a user initiated reset occurs, do everything in
            // this.reset and call it now and during a user initiated reset
            this.Reset();
        }

		public void left_wall_down() 
		{
			this.wall1.goToPosition (0.8f);
			//this.wall2.goToPosition (0);
			//this.testServo.goToPosition(0.9f);
		}

		public void left_wall_up()
		{
			//this.wall1.goToPosition (0.5f);
			//this.wall1.stop();
			//this.wall2.stop ();
			this.wall1.goToPosition (0.25f);
			//this.wall2.goToPosition (0.5f);
			//this.testServo.goToPosition(0.6f);
		}

		public void spinning_flashers_on()
		{
			this.flasherMotor1.goToPosition (0.65f);
			this.flasherMotor2.goToPosition (0.65f);
			this.flasherMotor3.goToPosition (0.65f);
		}

		public void spinning_flashers_off()
		{
			this.flasherMotor1.stop ();
			this.flasherMotor2.stop ();
			this.flasherMotor3.stop ();
		}

		public void test_servo()
		{
			for (uint pulselen = 150; pulselen < 600; pulselen++) {
				this.testServo.SetPwm (0, pulselen);
			}
			Thread.Sleep (500);
			for (uint pulselen = 600; pulselen > 150; pulselen--) {
				this.testServo.SetPwm (0, pulselen);
			}
		}

        public void on_ball_saved()
        {
            ball_being_saved = true;
        }

        public void set_status(string text, int seconds = 5)
        {
            this.dmd.set_message(text, seconds);
        }

        public override void Reset()
        {
            base.Reset();
            // Add the basic modes to the queue
            _modes.Add(attract);
            _modes.Add(ball_save);
            _modes.Add(trough);

            //_modes.Add(_ball_search);

            // Disable the flippers
            this.FlippersEnabled = false;
        }

        /// <summary>
        /// Empty callback just incase a ball drains into the trough before another
        /// drain_callback can be installed by a gameplay mode
        /// </summary>
        public void drain_callback()
        {
        }

        public override void ball_starting()
        {
            base.ball_starting();
            _modes.Add(_base_game_mode);
        }

        public override void ball_ended()
        {
            _modes.Remove(_base_game_mode);
            base.ball_ended();
        }

        public override void game_ended()
        {
            base.game_ended();
            _modes.Add(attract);
        }

        public void extra_ball()
        {
            Player p = current_player();
            p.extra_balls++;
        }

        public override void game_started()
        {
            lampctrl.stop_show();
            base.game_started();
        }

        public void setup_ball_search()
        {
        }

        public void close_divertor()
        {
            safe_drive_coil("divertorMain", 50);
            safe_drive_coil("divertorHold", 0);
        }

        public void open_divertor()
        {
            safe_disable_coil("divertorMain");
            safe_disable_coil("divertorHold");
        }

        public void all_gi_on()
        {
            foreach (Driver d in this.GI.Values)
            {
                d.Enable();
            }
        }
        public void all_gi_off()
        {
            foreach (Driver d in this.GI.Values)
            {
                d.Disable();
            }
        }
    }
}
