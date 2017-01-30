/*======================================
		Types ordering tests
======================================*/

USE `nz_test_closure`;

/*---------------------------------------/
			Reverse order
[Expected: (User, Vehicle), Admin, MegaAdmin]
--------------------------------------*/
CALL before_alter_types();
INSERT INTO `type_add_list`
(`name`, `table_name`, `base_type_name`)
VALUES
('VeryGoodPeople', '_very_good_people', 'GoodPeople'),
('GoodPeople', '_good_people', 'People'),
('People', '_people', NULL),
('Vehicle', '_vehicle', NULL);
CALL add_types();

/*---------------------------------------/
			Mixed order
[Expected: Group, (Car, Motocycle), (Chopper, HotRod, Sedan)]
--------------------------------------*/
CALL before_alter_types();
INSERT INTO `type_add_list`
(`name`, `table_name`, `base_type_name`)
VALUES
('CoolChopper', '_cool_chopper', 'Chopper'),
('HotRod', '_hot_rod', 'Car'),
('Chopper', '_chopper', 'Motocycle'),
('Car', '_car', 'Vehicle'),
('Motocycle', '_motocycle', 'Vehicle'),
('Buildng', '_building', NULL),
('Sedan', '_sedan', 'Car');
CALL add_types();
