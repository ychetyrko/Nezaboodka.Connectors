/*======================================
		Remove fields tests
 (based on `Add types and fields tests`
  and `Types ordering tests`)
======================================*/

USE `nz_test_closure`;

/*---------------------------------------/
	Remove single nested field
		without Back Reference
--------------------------------------*/
CALL before_alter_database_schema();
INSERT INTO `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('User', 'Login');
CALL alter_database_schema('nz_test_closure');

/*---------------------------------------/
	Remove single nested field
		with Back Reference
--------------------------------------*/
CALL before_alter_database_schema();
INSERT INTO `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('User', 'Group');
CALL alter_database_schema('nz_test_closure');

/*---------------------------------------/
	Remove single non-nested field
		with Back Reference
--------------------------------------*/
CALL before_alter_database_schema();
INSERT INTO `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('Group', 'Admin');
CALL alter_database_schema('nz_test_closure');

/*---------------------------------------/
	Remove multiple non-nested fields
		with(out) Back Reference
--------------------------------------*/
CALL before_alter_database_schema();
INSERT INTO `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('Admin', 'ControlGroup'),
('Group', 'Title'),
('UberAdmin', 'UberRating');
CALL alter_database_schema('nz_test_closure');

/*---------------------------------------/
	Remove multiple fields
		with one nonexisting
		[Error expected]
--------------------------------------*/
CALL before_alter_database_schema();
INSERT INTO `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('GoodPeople', 'Goodies'),
('People', 'Name'),
('Admin', 'Login');	-- removed before
CALL alter_database_schema('nz_test_closure');
