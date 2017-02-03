/*======================================
		Remove fields tests
 (based on `Add types and fields tests`)
======================================*/

USE `nz_test_closure`;

/*---------------------------------------/
	Remove single nested field
		without Back Reference
--------------------------------------*/
CALL before_alter_db_schema();
INSERT INTO `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('User', 'Login');
CALL alter_db_schema();

/*---------------------------------------/
	Remove single nested field
		with Back Reference
--------------------------------------*/
CALL before_alter_db_schema();
INSERT INTO `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('User', 'Group');
CALL alter_db_schema();

/*---------------------------------------/
	Remove single non-nested field
		with Back Reference
--------------------------------------*/
CALL before_alter_db_schema();
INSERT INTO `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('Group', 'Admin');
CALL alter_db_schema();

/*---------------------------------------/
	Remove multiple non-nested fields
		with(out) Back Reference
--------------------------------------*/
CALL before_alter_db_schema();
INSERT INTO `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('Admin', 'ControlGroup'),
('Group', 'Title'),
('UberAdmin', 'UberRating');
CALL alter_db_schema();

/*---------------------------------------/
	Remove nonexisting single field
		[Error expected]
--------------------------------------*/
CALL before_alter_db_schema();
INSERT INTO `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('Admin', 'Login');
CALL alter_db_schema();
