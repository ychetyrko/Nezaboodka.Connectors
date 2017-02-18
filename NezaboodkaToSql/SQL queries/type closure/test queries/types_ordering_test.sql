/*======================================
		Types ordering tests
======================================*/

USE `nz_test_closure`;

/*---------------------------------------/
			Reverse order
[Expected: (People, Vehicle), GoodPeople, VeryGoodPeople]
--------------------------------------*/
CALL before_alter_db_schema();
INSERT INTO `type_add_list`
(`name`, `table_name`, `base_type_name`)
VALUES
('VeryGoodPeople', '_very_good_people', 'GoodPeople'),
('GoodPeople', '_good_people', 'People'),
('People', '_people', NULL),
('Vehicle', '_vehicle', NULL);
CALL alter_db_schema('nz_test_closure');

/*---------------------------------------/
			Mixed order
[Expected: Group, (Car, Motocycle), (Chopper, HotRod, Sedan)]
--------------------------------------*/
CALL before_alter_db_schema();
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
CALL alter_db_schema('nz_test_closure');

/*---------------------------------------/
	Fields for further tests
--------------------------------------*/
CALL before_alter_db_schema();
INSERT INTO `field_add_list`
(`name`, `col_name`, `owner_type_name`, `type_name`, `is_list`, `compare_options`, `back_ref_name`)
VALUES
('Name', '_name', 'People', 'VARCHAR(50)', FALSE, 'IgnoreCase', NULL),
('Goodies', '_goodies', 'GoodPeople', 'TEXT', TRUE, 'None', NULL),
('Car', '_car', 'VeryGoodPeople', 'Car', FALSE, 'None', NULL);
CALL alter_db_schema('nz_test_closure');
