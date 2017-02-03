/*======================================
		Remove fields tests
 (based on `Types ordering tests`
  and `Add types and fields tests`)
======================================*/

USE `nz_test_closure`;

/*---------------------------------------/
		Remove full hierarchy
--------------------------------------*/
CALL before_alter_db_schema();
INSERT INTO `type_rem_list`
(`name`)
VALUES
('Moderator'),
('Admin'),
('UberAdmin'),
('Group'),
('User');
CALL alter_db_schema();

/*---------------------------------------/
		Remove terminating types
--------------------------------------*/
CALL before_alter_db_schema();
INSERT INTO `type_rem_list`
(`name`)
VALUES
('CoolChopper'),
('Sedan');
CALL alter_db_schema();

/*---------------------------------------/
		Remove referenced type
			[Error expected]
--------------------------------------*/
CALL before_alter_db_schema();
INSERT INTO `type_rem_list`
(`name`)
VALUES
('Car');	-- referenced by VeryGoodPeople
CALL alter_db_schema();
