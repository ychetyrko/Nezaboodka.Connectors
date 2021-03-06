/*********************************************

			Types ordering tests

**********************************************/

USE `nz_admin_db`;

/*---------------------------------------/
			Reverse order
[Expected: (People, Vehicle), GoodPeople, VeryGoodPeople]
--------------------------------------*/
CALL before_alter_database_schema();
INSERT INTO `type_add_list`
(`name`, `table_name`, `base_type_name`)
VALUES
('VeryGoodPeople', '_very_good_people', 'GoodPeople'),
('GoodPeople', '_good_people', 'People'),
('People', '_people', ''),
('Vehicle', '_vehicle', '');
CALL alter_database_schema('nz_test_db');

/*---------------------------------------/
			Mixed order
[Expected: Group, (Car, Motocycle), (Chopper, HotRod, Sedan)]
--------------------------------------*/
CALL before_alter_database_schema();
INSERT INTO `type_add_list`
(`name`, `table_name`, `base_type_name`)
VALUES
('CoolChopper', '_cool_chopper', 'Chopper'),
('HotRod', '_hot_rod', 'Car'),
('Chopper', '_chopper', 'Motocycle'),
('Car', '_car', 'Vehicle'),
('Motocycle', '_motocycle', 'Vehicle'),
('Buildng', '_building', ''),
('Sedan', '_sedan', 'Car');
CALL alter_database_schema('nz_test_db');

/*---------------------------------------/
		Fields for further tests
--------------------------------------*/
CALL before_alter_database_schema();
INSERT INTO `field_add_list`
(`name`, `col_name`, `owner_type_name`, `type_name`, `is_list`, `compare_options`)
VALUES
('Name', '_name', 'People', 'VARCHAR(50)', FALSE, 'IgnoreCase'),
('Goodies', '_goodies', 'GoodPeople', 'TEXT', TRUE, 'None'),
('Car', '_car', 'VeryGoodPeople', 'Car', FALSE, 'None');
CALL alter_database_schema('nz_test_db');

-- Get nz_test_db types
USE `nz_test_db`;

SELECT `name`, `base_type_name`
FROM `type`;
